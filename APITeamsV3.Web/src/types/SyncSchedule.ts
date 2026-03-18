export interface SyncSchedule {
    id: number;
    companyConfigId: number;
    companyName: string;
    daysOfWeek: string;
    hour: number;
    minute: number;
    isEnabled: boolean;
    lastRunAt: string | null;
}

export interface CreateSyncScheduleRequest {
    companyConfigId: number;
    daysOfWeek: string;
    hour: number;
    minute: number;
    isEnabled: boolean;
}
