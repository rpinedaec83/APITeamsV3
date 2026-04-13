export const MAINTENANCE_REQUIRED_EVENT = "apiteams:maintenance-required";

const DEFAULT_MESSAGE = "El sistema se encuentra en mantenimiento. Por favor, intente más tarde.";
const MAINTENANCE_MARKERS = ["mantenimiento", "maintenance"];

const normalize = (value: string) => value.trim().toLowerCase();

const extractMessage = (value: unknown): string | null => {
    if (typeof value === "string") {
        const message = value.trim();
        return message.length > 0 ? message : null;
    }

    if (value && typeof value === "object") {
        const candidate = value as { message?: unknown; Message?: unknown; detail?: unknown; Detail?: unknown };

        return extractMessage(candidate.message)
            ?? extractMessage(candidate.Message)
            ?? extractMessage(candidate.detail)
            ?? extractMessage(candidate.Detail);
    }

    return null;
};

export const isMaintenancePayload = (value: unknown): boolean => {
    const message = extractMessage(value);
    if (!message) {
        return false;
    }

    const normalized = normalize(message);
    return MAINTENANCE_MARKERS.some(marker => normalized.includes(marker));
};

export const getMaintenanceMessage = (value: unknown): string => {
    return extractMessage(value) ?? DEFAULT_MESSAGE;
};

export const notifyMaintenanceRequired = (message?: string) => {
    window.dispatchEvent(new CustomEvent(MAINTENANCE_REQUIRED_EVENT, {
        detail: { message: getMaintenanceMessage(message) }
    }));
};

export const installMaintenanceFetchInterceptor = () => {
    if ((window as Window & { __APITEAMSV3_FETCH_MAINTENANCE__?: boolean }).__APITEAMSV3_FETCH_MAINTENANCE__) {
        return;
    }

    const originalFetch = window.fetch.bind(window);

    window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
        const response = await originalFetch(input, init);

        if (response.status === 503) {
            let payload: unknown = null;

            try {
                const cloned = response.clone();
                const contentType = cloned.headers.get("content-type") ?? "";
                if (contentType.includes("application/json")) {
                    payload = await cloned.json();
                } else {
                    payload = await cloned.text();
                }
            } catch {
                payload = null;
            }

            if (isMaintenancePayload(payload)) {
                notifyMaintenanceRequired(getMaintenanceMessage(payload));
            }
        }

        return response;
    };

    (window as Window & { __APITEAMSV3_FETCH_MAINTENANCE__?: boolean }).__APITEAMSV3_FETCH_MAINTENANCE__ = true;
};
