export const getApiErrorMessage = (error: unknown, fallback: string): string => {
    if (typeof error !== 'object' || error === null) {
        return fallback;
    }

    const candidate = error as {
        message?: unknown;
        response?: {
            data?: {
                message?: unknown;
                Message?: unknown;
                detail?: unknown;
                Detail?: unknown;
            } | string;
        };
    };

    const responseData = candidate.response?.data;

    if (typeof responseData === 'string' && responseData.trim().length > 0) {
        return responseData.trim();
    }

    if (responseData && typeof responseData === 'object') {
        const message = responseData.message ?? responseData.Message;
        const detail = responseData.detail ?? responseData.Detail;

        if (typeof message === 'string' && message.trim().length > 0) {
            if (typeof detail === 'string' && detail.trim().length > 0) {
                return `${message.trim()} Detalle: ${detail.trim()}`;
            }

            return message.trim();
        }

        if (typeof detail === 'string' && detail.trim().length > 0) {
            return detail.trim();
        }
    }

    if (typeof candidate.message === 'string' && candidate.message.trim().length > 0) {
        return candidate.message.trim();
    }

    return fallback;
};
