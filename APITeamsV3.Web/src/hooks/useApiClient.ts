import { useMsal, useAccount } from "@azure/msal-react";
import axios from "axios";
import { loginRequest } from "../authConfig";
import { useEffect, useMemo } from "react";

export const useApiClient = () => {
    const { instance, accounts } = useMsal();
    const account = useAccount(accounts[0] || {});

    const client = useMemo(() => {
        const api = axios.create({
            baseURL: DetermineApiUrl(), // Helper function or direct logic
        });

        api.interceptors.request.use(async (config) => {
            if (account) {
                try {
                    const response = await instance.acquireTokenSilent({
                        ...loginRequest,
                        account: account
                    });
                    config.headers.Authorization = `Bearer ${response.accessToken}`;
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

// Helper to determine API URL based on host
const DetermineApiUrl = () => {
    if (window.location.hostname.includes('localhost')) {
        return 'http://localhost:5000/api'; // Use http for local dev for now as https might have cert issues
    } else {
        return `https://api.${window.location.hostname}/api`;
    }
}
