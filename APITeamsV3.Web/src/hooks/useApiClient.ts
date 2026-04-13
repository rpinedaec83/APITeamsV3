import { useMsal, useAccount } from "@azure/msal-react";
import axios from "axios";
import { getApiScopes } from "../authConfig";
import { useMemo } from "react";
import { getBaseApiUrl } from "../utils/config";
import { CacheLookupPolicy, InteractionRequiredAuthError } from "@azure/msal-browser";
import { getMaintenanceMessage, isMaintenancePayload, notifyMaintenanceRequired } from "../utils/maintenance";

export const AUTH_INTERACTION_REQUIRED_EVENT = "apiteams:auth-interaction-required";
export const AUTH_TOKEN_REQUIRED_ERROR = "auth_token_required";

const shouldRequestInteractiveAuth = () => window.self === window.top;

const notifyInteractiveAuthRequired = () => {
    if (!shouldRequestInteractiveAuth()) {
        return;
    }

    window.dispatchEvent(new CustomEvent(AUTH_INTERACTION_REQUIRED_EVENT));
};

const createAuthTokenRequiredError = () => {
    const error = new Error("Interactive authentication is required.");
    error.name = AUTH_TOKEN_REQUIRED_ERROR;
    return error;
};

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
                        account: account,
                        cacheLookupPolicy: CacheLookupPolicy.AccessTokenAndRefreshToken,
                    });
                    config.headers.Authorization = `Bearer ${response.accessToken}`;
                } catch (error) {
                    console.error("Token acquisition failed", error);
                    if (error instanceof InteractionRequiredAuthError) {
                        notifyInteractiveAuthRequired();
                        throw createAuthTokenRequiredError();
                    }

                    throw error;
                }
            }
            return config;
        });

        api.interceptors.response.use(
            (response) => response,
            async (error) => {
                if (error instanceof Error && error.name === AUTH_TOKEN_REQUIRED_ERROR) {
                    return Promise.reject(error);
                }

                if (error.response && error.response.status === 401) {
                    console.warn("Unauthorized request, interactive login required.");
                    notifyInteractiveAuthRequired();
                }

                if (error.response && error.response.status === 503) {
                    if (isMaintenancePayload(error.response.data)) {
                        notifyMaintenanceRequired(getMaintenanceMessage(error.response.data));
                    }
                }

                return Promise.reject(error);
            }
        );

        return api;
    }, [instance, account]);

    return client;
};

// Utility now centrally managed in src/utils/config.ts
