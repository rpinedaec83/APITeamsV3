import { useMsal, useAccount } from "@azure/msal-react";
import axios from "axios";
import { getApiScopes, getLoginRequest } from "../authConfig";
import { useMemo } from "react";
import { getBaseApiUrl } from "../utils/config";
import { InteractionRequiredAuthError } from "@azure/msal-browser";

export const useApiClient = () => {
    const { instance, accounts } = useMsal();
    const account = useAccount(accounts[0] || {});

    const client = useMemo(() => {
        const api = axios.create({
            baseURL: getBaseApiUrl(),
        });

        api.interceptors.request.use(async (config) => {
            if (account) {
                const apiScopes = getApiScopes(window.__APITEAMSV3_CONFIG__);
                if (apiScopes.length === 0) {
                    throw new Error("API client configuration is missing.");
                }

                try {
                    const response = await instance.acquireTokenSilent({
                        scopes: apiScopes,
                        account: account
                    });
                    config.headers.Authorization = `Bearer ${response.accessToken}`;
                } catch (error) {
                    console.error("Token acquisition failed", error);
                    if (error instanceof InteractionRequiredAuthError) {
                        instance.loginRedirect(getLoginRequest());
                    }
                }
            }
            return config;
        });

        api.interceptors.response.use(
            (response) => response,
            (error) => {
                if (error.response && error.response.status === 401) {
                    console.warn("Unauthorized request, redirecting to login...");
                    instance.loginRedirect(getLoginRequest());
                }
                return Promise.reject(error);
            }
        );

        return api;
    }, [instance, account]);

    return client;
};

// Utility now centrally managed in src/utils/config.ts
