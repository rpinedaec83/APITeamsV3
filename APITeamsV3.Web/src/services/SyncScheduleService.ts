import type { SyncSchedule, CreateSyncScheduleRequest } from '../types/SyncSchedule';

const getApiUrl = () => {
    if (window.location.hostname.includes('localhost')) {
        return 'http://localhost:5000/api/admin/sync-schedules';
    }
    return `https://api.${window.location.hostname}/api/admin/sync-schedules`;
};

const getHeaders = () => {
    const headers = new Headers();
    headers.append('Content-Type', 'application/json');
    return headers;
};

export const SyncScheduleService = {
    getAll: async (): Promise<SyncSchedule[]> => {
        const response = await fetch(getApiUrl(), { headers: getHeaders() });
        if (!response.ok) throw new Error('Failed to fetch schedules');
        return response.json();
    },

    create: async (data: CreateSyncScheduleRequest): Promise<number> => {
        const response = await fetch(getApiUrl(), {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data)
        });
        if (!response.ok) throw new Error('Failed to create schedule');
        return response.json();
    },

    update: async (id: number, data: CreateSyncScheduleRequest & { id: number }): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data)
        });
        if (!response.ok) throw new Error('Failed to update schedule');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}`, {
            method: 'DELETE',
            headers: getHeaders()
        });
        if (!response.ok) throw new Error('Failed to delete schedule');
    },

    toggle: async (id: number, isEnabled: boolean): Promise<void> => {
        const response = await fetch(`${getApiUrl()}/${id}/toggle`, {
            method: 'PATCH',
            headers: getHeaders(),
            body: JSON.stringify({ isEnabled })
        });
        if (!response.ok) throw new Error('Failed to toggle schedule');
    }
};
