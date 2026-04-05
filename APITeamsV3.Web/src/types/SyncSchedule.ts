export interface SyncSchedule {
    id: number;
    companyConfigId: number;
    companyName: string;
    timeZoneId: string;
    daysOfWeek: string;
    hour: number;
    minute: number;
    isEnabled: boolean;
    createdAt: string;
    lastRunAt: string | null;
}

export interface CreateSyncScheduleRequest {
    companyConfigId: number;
    daysOfWeek: string;
    hour: number;
    minute: number;
    isEnabled: boolean;
}
