import { useMsal, useAccount } from "@azure/msal-react";
import axios from "axios";
import { loginRequest } from "../authConfig";
import { useMemo } from "react";
import { getBaseApiUrl } from "../utils/config";

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
                    // Fallback to interaction if silent fails? 
                    // Usually we redirect to login or handle error
                    console.error("Token acquisition failed", error);
                }
            }
            return config;
        });

        return api;
    }, [instance, account]);

    return client;
};

// Utility now centrally managed in src/utils/config.ts
