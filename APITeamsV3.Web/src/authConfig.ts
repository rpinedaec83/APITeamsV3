import { LogLevel, type Configuration } from "@azure/msal-browser";

export const createMsalConfig = (clientId: string, tenantId: string, redirectUri: string): Configuration => {
    return {
        auth: {
            clientId: clientId,
            authority: `https://login.microsoftonline.com/${tenantId}`,
            redirectUri: redirectUri,
            postLogoutRedirectUri: window.location.origin,
            // navigateToLoginRequestUrl: true, // Linter error: property does not exist in type
        },
        cache: {
            cacheLocation: "sessionStorage", // This configures where your cache will be stored
            // storeAuthStateInCookie: false, // Set this to "true" if you are having issues on IE11 or Edge. Linter error: property does not exist
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
                            // console.info(message);
                            return;
                        case LogLevel.Verbose:
                            // console.debug(message);
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

export const loginRequest = {
    scopes: ["User.Read", "Directory.Read.All", "Group.ReadWrite.All"] // Adjust scopes as needed
};
