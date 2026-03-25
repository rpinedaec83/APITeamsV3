import { LogLevel, type Configuration, type RedirectRequest } from "@azure/msal-browser";

export interface SpaBootstrapConfig {
    spaClientId: string;
    tenantId: string;
    companyKey: string;
    apiClientId: string;
    apiScopes: string[];
}

declare global {
    interface Window {
        __APITEAMSV3_CONFIG__?: SpaBootstrapConfig;
    }
}

export const createMsalConfig = (clientId: string, tenantId: string, redirectUri: string): Configuration => {
    return {
        auth: {
            clientId: clientId,
            authority: `https://login.microsoftonline.com/${tenantId}`,
            redirectUri: redirectUri,
            postLogoutRedirectUri: window.location.origin,
        },
        cache: {
            cacheLocation: "sessionStorage",
        },
        system: {
            loggerOptions: {
                loggerCallback: (level, message, containsPii) => {
                    if (containsPii) {
                        return;
                    }

                    switch (level) {
                        case LogLevel.Error:
                            console.error(message);
                            return;
                        case LogLevel.Info:
                            return;
                        case LogLevel.Verbose:
                            return;
                        case LogLevel.Warning:
                            console.warn(message);
                            return;
                    }
                }
            }
        }
    };
};

export const getApiScopes = (config?: Pick<SpaBootstrapConfig, "apiClientId" | "apiScopes">): string[] => {
    if (config?.apiScopes?.length) {
        return config.apiScopes;
    }

    if (!config?.apiClientId) {
        return [];
    }

    return [`api://${config.apiClientId}/access_as_user`];
};

export const getLoginRequest = (): RedirectRequest => ({
    scopes: getApiScopes(window.__APITEAMSV3_CONFIG__)
});
