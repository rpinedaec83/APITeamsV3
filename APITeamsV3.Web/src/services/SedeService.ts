import type { Sede } from '../types/Sede';
import { getBaseApiUrl } from '../utils/config';

const getApiUrl = () => `${getBaseApiUrl()}/admin/sedes`;

const getHeaders = () => {
    const headers = new Headers();
    headers.append('Content-Type', 'application/json');
    return headers;
};

export const SedeService = {
    getByCompany: async (companyConfigId: number): Promise<Sede[]> => {
        const response = await fetch(`${getApiUrl()}/${companyConfigId}`, { headers: getHeaders() });
        if (!response.ok) throw new Error('Failed to fetch sedes');
        return response.json();
    },

    importFromSmart: async (companyConfigId: number): Promise<Sede[]> => {
        const response = await fetch(`${getApiUrl()}/import/${companyConfigId}`, {
            method: 'POST',
            headers: getHeaders()
        });
        if (!response.ok) throw new Error('Failed to import sedes');
        return response.json();
    },

    toggleActive: async (id: number, isActive: boolean): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}/toggle`, {
            method: 'PATCH',
            headers: getHeaders(),
            body: JSON.stringify({ isActive })
        });
        if (!response.ok) throw new Error('Failed to toggle sede');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}`, {
            method: 'DELETE',
            headers: getHeaders()
        });
        if (!response.ok) throw new Error('Failed to delete sede');
    },

    getActiveCodes: async (companyConfigId: number): Promise<string> => {
        const response = await fetch(`${getApiUrl()}/${companyConfigId}/active-codes`, { headers: getHeaders() });
        if (!response.ok) throw new Error('Failed to fetch active codes');
        const data = await response.json();
        return data.codes;
    }
};
