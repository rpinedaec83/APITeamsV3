/**
 * Determines the base API URL based on the current hostname.
 * This ensures that local development uses localhost and production use the appropriate domain.
 */
export const getBaseApiUrl = () => {
    if (window.location.hostname.includes('localhost')) {
        return 'http://localhost:5000/api';
    }
    return `https://api.${window.location.hostname}/api`;
};
