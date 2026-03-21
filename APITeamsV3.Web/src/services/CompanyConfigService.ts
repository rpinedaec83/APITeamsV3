import type { CompanyConfig, CreateCompanyConfigRequest, UpdateCompanyConfigRequest } from '../types/CompanyConfig';
import { getBaseApiUrl } from '../utils/config';

// Helper to get API URL
const getApiUrl = () => `${getBaseApiUrl()}/admin/company-configs`;

// Helper for headers (simple for now, ideally includes auth token)
const getHeaders = () => {
    const headers = new Headers();
    headers.append('Content-Type', 'application/json');
    // Add Authorization header here if using MSAL token
    return headers;
};

export const CompanyConfigService = {
    getAll: async (): Promise<CompanyConfig[]> => {
        const response = await fetch(getApiUrl(), { headers: getHeaders() });
        if (!response.ok) throw new Error('Failed to fetch configs');
        return response.json();
    },

    getById: async (id: number): Promise<CompanyConfig> => {
        const response = await fetch(`${getApiUrl()}/${id}`, { headers: getHeaders() });
        if (!response.ok) throw new Error('Failed to fetch config');
        return response.json();
    },

    create: async (request: CreateCompanyConfigRequest): Promise<number> => {
        const response = await fetch(getApiUrl(), {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(request)
        });
        if (!response.ok) throw new Error('Failed to create config');
        return response.json();
    },

    update: async (id: number, request: UpdateCompanyConfigRequest): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(request)
        });
        if (!response.ok) throw new Error('Failed to update config');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}`, {
            method: 'DELETE',
            headers: getHeaders()
        });
        if (!response.ok) throw new Error('Failed to delete config');
    }
};
