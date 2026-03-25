import { useMsal, useAccount } from "@azure/msal-react";
import axios from "axios";
import { loginRequest } from "../authConfig";
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
                try {
                    const response = await instance.acquireTokenSilent({
                        ...loginRequest,
                        account: account
                    });
                    config.headers.Authorization = `Bearer ${response.idToken}`;
                } catch (error) {
                    console.error("Token acquisition failed", error);
                    // If silent acquisition fails, it's often because session expired or interaction is required
                    if (error instanceof InteractionRequiredAuthError) {
                        instance.loginRedirect(loginRequest);
                    }
                }
            }
            return config;
        });

        api.interceptors.response.use(
            (response) => response,
            (error) => {
                // If API returns 401, redirect to login
                if (error.response && error.response.status === 401) {
                    console.warn("Unauthorized request, redirecting to login...");
                    instance.loginRedirect(loginRequest);
                }
                return Promise.reject(error);
            }
        );

        return api;
    }, [instance, account]);

    return client;
};

// Utility now centrally managed in src/utils/config.ts
