export interface CompanyConfig {
    id: number;
    companyKey: string;
    displayName: string;
    frontHost: string;
    apiHost: string;
    spaClientId?: string;
    spaTenantId?: string;
    smartConnectionString?: string; // Optional/Masked
    timeZoneId: string;
    isActive: boolean;
    graphTenantId: string;
    graphClientId: string;
    graphClientSecretRef: string;
    defaultChannelName: string;
    meetingPolicyMode: string;
    isPilotMode: boolean;
    pilotSections: number[];
    teacherAltDomain?: string;
    isMaintenanceMode: boolean;
    maintenanceMessage?: string;
}

export interface CreateCompanyConfigRequest {
    companyKey: string;
    displayName: string;
    frontHost: string;
    apiHost: string;
    spaClientId?: string;
    smartConnectionString: string;
    timeZoneId: string;
    isActive: boolean;
    graphTenantId: string;
    graphClientId: string;
    graphClientSecretRef: string;
    isPilotMode: boolean;
    pilotSections: number[];
    teacherAltDomain?: string;
    isMaintenanceMode: boolean;
    maintenanceMessage?: string;
}

export interface UpdateCompanyConfigRequest {
    companyKey: string;
    displayName: string;
    frontHost: string;
    apiHost: string;
    spaClientId?: string;
    smartConnectionString?: string;
    timeZoneId: string;
    isActive: boolean;
    graphTenantId: string;
    graphClientId: string;
    graphClientSecretRef: string;
    isPilotMode: boolean;
    pilotSections: number[];
    teacherAltDomain?: string;
    isMaintenanceMode: boolean;
    maintenanceMessage?: string;
}
