import type { SelectTabData, TabValue } from '@fluentui/react-components';
import React, { useEffect, useState } from 'react';
import {
    makeStyles,
    shorthands,
    Button,
    Input,
    Label,
    Title3,
    Divider,
    Table,
    TableHeader,
    TableRow,
    TableHeaderCell,
    TableBody,
    TableCell,
    tokens,
    Avatar,
    Badge,
    TabList,
    Tab,
    Spinner,
    Text,
    Tooltip,
    Switch,
} from '@fluentui/react-components';
import {
    PlayRegular,
    ArrowSyncRegular,
    SearchRegular,
    PeopleRegular,
    PersonDeleteRegular,
    RenameRegular,
    CalendarRegular,
    CalendarSyncRegular,
    PersonRegular,
    TimerRegular,
    CheckmarkCircleRegular,
    ErrorCircleRegular,
    InfoRegular,
    ClockRegular,
    DismissCircleRegular,
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';
import { showConfirm, showSuccess, showWarning } from '../utils/alerts';
import { getApiErrorMessage } from '../utils/apiErrors';

interface HangfireJobResult {
    jobId: string;
    companyKey: string;
    state: string;
    method: string;
    idSeccion?: number | null;
    sectionCode?: string | null;
    timestamp: string;
    arguments: string[];
    error?: string;
}

interface HangfireRecurringJobResult {
    id: string;
    companyKey: string;
    cron: string;
    queue: string;
    method: string;
    idSeccion?: number | null;
    sectionCode?: string | null;
    createdAt?: string | null;
    lastExecution?: string | null;
    nextExecution?: string | null;
    lastJobId: string;
    lastJobState: string;
    lastResult: string;
    timeZoneId: string;
    error: string;
    removed: boolean;
}

interface RecordingTransferPilotJobConfig {
    isEnabled: boolean;
    cron: string;
    timeZoneId: string;
    isPilotMode: boolean;
    pilotSectionsConfigured: number;
}

interface TenantConfigResponse {
    timeZoneId?: string;
}

interface HangfireStats {
    enqueued: number;
    processing: number;
    succeeded: number;
    failed: number;
    scheduled: number;
    deleted: number;
    recurring: number;
}

const WINDOWS_TO_IANA_TIMEZONES: Record<string, string> = {
    'SA Pacific Standard Time': 'America/Lima',
    'Pacific SA Standard Time': 'America/Santiago',
    'Eastern Standard Time': 'America/New_York',
    'SA Western Standard Time': 'America/La_Paz',
    UTC: 'UTC',
};

const useStyles = makeStyles({
    root: {
        padding: '32px',
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        maxWidth: '1400px',
        margin: '0 auto',
        backgroundColor: tokens.colorNeutralBackground2,
        minHeight: '100vh',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '16px',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px', '24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow4,
    },
    headerTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        color: tokens.colorBrandForeground1,
    },
    mainCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow2,
        transition: 'transform 0.2s, box-shadow 0.2s',
        ':hover': {
            boxShadow: tokens.shadow8,
        },
    },
    sectionInput: {
        display: 'flex',
        gap: '16px',
        alignItems: 'end',
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.padding('16px'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
    },
    inputGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        flexGrow: 1,
        maxWidth: '300px',
    },
    jobCardsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
        gap: '16px',
        marginTop: '16px',
    },
    jobCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        transition: 'all 0.2s ease',
        ':hover': {
            boxShadow: tokens.shadow8,
            borderTopColor: tokens.colorBrandStroke1,
            borderRightColor: tokens.colorBrandStroke1,
            borderBottomColor: tokens.colorBrandStroke1,
            borderLeftColor: tokens.colorBrandStroke1,
            transform: 'translateY(-2px)',
        },
    },
    jobCardHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
    },
    jobCardIcon: {
        width: '40px',
        height: '40px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        fontSize: '20px',
    },
    jobCardTitle: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground1,
    },
    jobCardDescription: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.4',
    },
    historyContainer: {
        marginTop: '32px',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
        boxShadow: tokens.shadow2,
    },
    sectionHeader: {
        padding: '16px 24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '12px',
    },
    emptyState: {
        textAlign: 'center' as const,
        padding: '40px 20px',
        color: tokens.colorNeutralForeground3,
    },
    mono: {
        fontFamily: 'monospace',
        fontSize: '12px',
    },
    summaryGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
        gap: '16px',
        marginBottom: '8px',
    },
    summaryCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('16px', '20px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow4,
        display: 'flex',
        alignItems: 'center',
        gap: '16px',
        borderLeft: '4px solid transparent',
        transition: 'all 0.2s ease',
        ':hover': {
            transform: 'translateY(-2px)',
            boxShadow: tokens.shadow8,
        },
    },
    summaryContent: {
        display: 'flex',
        flexDirection: 'column',
    },
    summaryValue: {
        fontSize: '24px',
        fontWeight: tokens.fontWeightBold,
        lineHeight: '1',
    },
    summaryLabel: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        fontWeight: tokens.fontWeightSemibold,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
    },
});

interface JobDefinition {
    key: string;
    label: string;
    description: string;
    endpoint: string;
    icon: React.ReactNode;
    color: string;
    category: 'sync' | 'schedule';
}

const JOB_DEFINITIONS: JobDefinition[] = [
    {
        key: 'sync-missing-students',
        label: 'Sinc. Alumnos Faltantes',
        description: 'Busca alumnos en Smart que aún no están en el Team.',
        endpoint: '/jobs/sync-missing-students',
        icon: <PeopleRegular />,
        color: tokens.colorPaletteBlueBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-obsolete-students',
        label: 'Sinc. Alumnos Obsoletos',
        description: 'Busca alumnos en Team que ya no están matriculados.',
        endpoint: '/jobs/sync-obsolete-students',
        icon: <PersonDeleteRegular />,
        color: tokens.colorPaletteRedBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-renamed-teams',
        label: 'Sinc. Equipos Renombrados',
        description: 'Alinea nombre y descripción de Teams con Smart.',
        endpoint: '/jobs/sync-renamed-teams',
        icon: <RenameRegular />,
        color: tokens.colorPaletteMarigoldBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-facilitator',
        label: 'Sinc. Facilitador',
        description: 'Alinea facilitador de Smart con owners del Team.',
        endpoint: '/jobs/sync-facilitator',
        icon: <PersonRegular />,
        color: tokens.colorPalettePurpleBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-roster',
        label: 'Sinc. Operativa Completa',
        description: 'Crea o actualiza el Team, sincroniza docentes y alumnos, y ajusta la agenda sin regenerarla manualmente.',
        endpoint: '/jobs/sync-roster',
        icon: <ArrowSyncRegular />,
        color: tokens.colorPaletteTealBorderActive,
        category: 'sync',
    },
    {
        key: 'generate-schedule',
        label: 'Generar Agendas',
        description: 'Genera agendas en Teams para la sección.',
        endpoint: '/jobs/generate-schedule',
        icon: <CalendarRegular />,
        color: tokens.colorPaletteGreenBorderActive,
        category: 'schedule',
    },
    {
        key: 'sync-dates',
        label: 'Sinc. Fechas',
        description: 'Sincroniza fechas de sesiones Teams con Smart.',
        endpoint: '/jobs/sync-dates',
        icon: <CalendarSyncRegular />,
        color: tokens.colorPaletteBerryBorderActive,
        category: 'schedule',
    },
];

const JobsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    const [idSeccion, setIdSeccion] = useState('');
    const [loading, setLoading] = useState<string | null>(null);
    const [loadingHistory, setLoadingHistory] = useState(false);
    const [loadingRecurring, setLoadingRecurring] = useState(false);
    const [loadingPilotTransfers, setLoadingPilotTransfers] = useState(false);
    const [purgingCompanyData, setPurgingCompanyData] = useState(false);
    const [jobIdLookup, setJobIdLookup] = useState('');
    const [requeueingJobId, setRequeueingJobId] = useState<string | null>(null);
    const [cancellingJobId, setCancellingJobId] = useState<string | null>(null);
    const [history, setHistory] = useState<HangfireJobResult[]>([]);
    const [recurringJobs, setRecurringJobs] = useState<HangfireRecurringJobResult[]>([]);
    const [selectedTab, setSelectedTab] = useState<TabValue>('all');
    const [recordingConfig, setRecordingConfig] = useState<RecordingTransferPilotJobConfig | null>(null);
    const [recordingCronDraft, setRecordingCronDraft] = useState('');
    const [loadingRecordingConfig, setLoadingRecordingConfig] = useState(false);
    const [savingRecordingConfig, setSavingRecordingConfig] = useState(false);
    const [tenantTimeZone, setTenantTimeZone] = useState('America/Lima');
    const [stats, setStats] = useState<HangfireStats | null>(null);
    const [loadingStats, setLoadingStats] = useState(false);

    const loadRecentJobs = async (silent = false) => {
        if (!silent) setLoadingHistory(true);
        try {
            const response = await apiClient.get('/jobs/recent', { params: { take: 100 } });
            const jobs = Array.isArray(response.data) ? response.data as HangfireJobResult[] : [];
            setHistory(jobs);
        } catch (err: unknown) {
            if (!silent) {
                const errorMessage = getApiErrorMessage(err, 'No se pudo cargar historial de Hangfire.');
                showWarning(errorMessage);
            }
        } finally {
            if (!silent) setLoadingHistory(false);
        }
    };

    const loadStats = async (silent = false) => {
        if (!silent) setLoadingStats(true);
        try {
            const response = await apiClient.get('/jobs/stats');
            setStats(response.data as HangfireStats);
        } catch (err: unknown) {
            console.error('Failed to load stats', err);
        } finally {
            if (!silent) setLoadingStats(false);
        }
    };

    const loadRecurringJobs = async (silent = false) => {
        if (!silent) setLoadingRecurring(true);
        try {
            const response = await apiClient.get('/jobs/recurring');
            const jobs = Array.isArray(response.data) ? response.data as HangfireRecurringJobResult[] : [];
            setRecurringJobs(jobs);
        } catch (err: unknown) {
            if (!silent) {
                const errorMessage = getApiErrorMessage(err, 'No se pudieron cargar los jobs recurrentes.');
                showWarning(errorMessage);
            }
        } finally {
            if (!silent) setLoadingRecurring(false);
        }
    };

    const handleRunPilotTransfers = async () => {
        setLoadingPilotTransfers(true);
        try {
            await apiClient.post('/jobs/recordings-transfer-pilot/run', {});
            await loadRecentJobs(true);
            await loadRecurringJobs(true);
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'No se pudo encolar la transferencia piloto.');
            showWarning(errorMessage);
        } finally {
            setLoadingPilotTransfers(false);
        }
    };

    const loadRecordingConfig = async (silent = false) => {
        if (!silent) setLoadingRecordingConfig(true);
        try {
            const response = await apiClient.get('/jobs/recordings-transfer-pilot/config');
            const config = response.data as RecordingTransferPilotJobConfig;
            setRecordingConfig(config);
            setRecordingCronDraft(config.cron ?? '');
        } catch (err: unknown) {
            if (!silent) {
                const errorMessage = getApiErrorMessage(err, 'No se pudo cargar la configuracion del job de grabaciones.');
                showWarning(errorMessage);
            }
        } finally {
            if (!silent) setLoadingRecordingConfig(false);
        }
    };

    const loadTenantConfig = async () => {
        try {
            const response = await apiClient.get('/config');
            const config = (response.data ?? {}) as TenantConfigResponse;
            const timeZoneId = config.timeZoneId ?? '';
            setTenantTimeZone((WINDOWS_TO_IANA_TIMEZONES[timeZoneId] ?? timeZoneId) || 'America/Lima');
        } catch {
            setTenantTimeZone('America/Lima');
        }
    };

    const handleSaveRecordingConfig = async () => {
        if (!recordingConfig) return;

        const cron = recordingCronDraft.trim();
        if (recordingConfig.isEnabled && !cron) {
            showWarning('Debe ingresar un CRON cuando el job esta habilitado.');
            return;
        }

        setSavingRecordingConfig(true);
        try {
            const response = await apiClient.put('/jobs/recordings-transfer-pilot/config', {
                isEnabled: recordingConfig.isEnabled,
                cron: cron || null,
            });

            const updated = response.data as RecordingTransferPilotJobConfig;
            setRecordingConfig(updated);
            setRecordingCronDraft(updated.cron ?? '');
            await loadRecurringJobs(true);
            await showSuccess('Configuracion del job de grabaciones guardada.');
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'No se pudo guardar la configuracion del job de grabaciones.');
            showWarning(errorMessage);
        } finally {
            setSavingRecordingConfig(false);
        }
    };

    const handleLookupJob = async () => {
        const normalizedJobId = jobIdLookup.trim();
        if (!normalizedJobId) {
            showWarning('Ingrese un Job ID.');
            return;
        }

        setLoadingHistory(true);
        try {
            const response = await apiClient.get(`/jobs/by-id/${encodeURIComponent(normalizedJobId)}`);
            const job = response.data as HangfireJobResult;
            setHistory(job ? [job] : []);
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'No se pudo consultar el Job ID.');
            showWarning(errorMessage);
        } finally {
            setLoadingHistory(false);
        }
    };

    const canRequeueJob = (state: string) => {
        const normalizedState = state.toLowerCase();
        return normalizedState.includes('fail') || normalizedState.includes('delet');
    };

    const handleRequeueJob = async (jobId: string) => {
        setRequeueingJobId(jobId);
        try {
            await apiClient.post(`/jobs/${encodeURIComponent(jobId)}/requeue`, {});
            await loadRecentJobs(true);
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'No se pudo re-encolar el job.');
            showWarning(errorMessage);
        } finally {
            setRequeueingJobId(null);
        }
    };

    const canCancelJob = (state: string) => {
        const normalizedState = state.toLowerCase();
        return normalizedState.includes('process') || normalizedState.includes('enqueu');
    };

    const handleCancelJob = async (jobId: string) => {
        const result = await showConfirm('¿Estás seguro de detener este job?');
        if (!result.isConfirmed) return;

        setCancellingJobId(jobId);
        try {
            await apiClient.delete(`/jobs/${encodeURIComponent(jobId)}`);
            await loadRecentJobs(true);
            showSuccess('Job detenido/eliminado exitosamente.');
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'No se pudo detener el job.');
            showWarning(errorMessage);
        } finally {
            setCancellingJobId(null);
        }
    };

    const handlePurgeCompanyData = async () => {
        const confirmation = await showConfirm(
            'Se eliminarán todos los jobs de Hangfire, el historial de ejecuciones y los logs operativos de la empresa actual.',
            'Borrar datos de la empresa');

        if (!confirmation.isConfirmed) {
            return;
        }

        setPurgingCompanyData(true);
        try {
            const response = await apiClient.delete('/jobs/company-data');
            const message = response?.data?.message || 'Se eliminaron jobs y logs de la empresa actual.';
            await showSuccess(message, 'Limpieza completada');
            setJobIdLookup('');
            await loadRecentJobs(true);
            await loadRecurringJobs(true);
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'No se pudo limpiar la empresa actual.');
            showWarning(errorMessage);
        } finally {
            setPurgingCompanyData(false);
        }
    };

    useEffect(() => {
        void loadRecentJobs(false);
        void loadRecurringJobs(false);
        void loadRecordingConfig(false);
        void loadStats(false);
        void loadTenantConfig();
        const interval = window.setInterval(() => {
            void loadRecentJobs(true);
            void loadRecurringJobs(true);
            void loadStats(true);
        }, 15000);

        return () => window.clearInterval(interval);
    }, [apiClient]);

    const handleEnqueueJob = async (job: JobDefinition) => {
        if (!idSeccion || !idSeccion.trim()) {
            showWarning('Ingrese un ID de Seccion valido.');
            return;
        }

        const seccionId = parseInt(idSeccion, 10);
        if (isNaN(seccionId)) {
            showWarning('El ID de Seccion debe ser numerico.');
            return;
        }

        setLoading(job.key);
        try {
            const url = job.key === 'sync-roster'
                ? `${job.endpoint}/${seccionId}?fullSync=true`
                : `${job.endpoint}/${seccionId}`;

            await apiClient.post(url, {});
            await loadRecentJobs(true);
        } catch (err: unknown) {
            const errorMessage = getApiErrorMessage(err, 'Error desconocido');
            showWarning(errorMessage);
        } finally {
            setLoading(null);
        }
    };

    const filteredJobs = selectedTab === 'all'
        ? JOB_DEFINITIONS
        : JOB_DEFINITIONS.filter(j => j.category === selectedTab);

    const onTabSelect = (_: unknown, data: SelectTabData) => {
        if (data?.value) setSelectedTab(data.value);
    };

    const getStateBadgeColor = (state: string) => {
        const normalized = state.toLowerCase();
        if (normalized.includes('succeed')) return 'success';
        if (normalized.includes('fail')) return 'danger';
        if (normalized.includes('process')) return 'brand';
        if (normalized.includes('schedul')) return 'warning';
        return 'informative';
    };

    const parseUtcDate = (value: string): Date => {
        if (!value) return new Date(Number.NaN);
        const normalized = /z$|[+-]\d{2}:\d{2}$/i.test(value) ? value : `${value}Z`;
        return new Date(normalized);
    };

    const formatDateTime = (value?: string | null) => {
        if (!value) return '-';
        const date = parseUtcDate(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return new Intl.DateTimeFormat('es-PE', {
            timeZone: tenantTimeZone,
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: true,
        }).format(date);
    };

    const getStateIcon = (state: string) => {
        const normalized = state.toLowerCase();
        if (normalized.includes('succeed')) {
            return <CheckmarkCircleRegular style={{ color: tokens.colorPaletteGreenForeground1, fontSize: '20px' }} />;
        }
        if (normalized.includes('fail')) {
            return <ErrorCircleRegular style={{ color: tokens.colorPaletteRedForeground1, fontSize: '20px' }} />;
        }
        if (normalized.includes('process')) {
            return <Spinner size="tiny" />;
        }
        return <ClockRegular style={{ color: tokens.colorNeutralForeground3, fontSize: '20px' }} />;
    };

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div className={styles.headerTitle}>
                    <Avatar color="brand" icon={<TimerRegular />} size={48} />
                    <div>
                        <Title3>Tareas Programadas (Hangfire)</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>
                            Ejecuta tareas y revisa estado real de los procesos en segundo plano
                        </div>
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <Badge appearance="filled" color="informative" size="large">
                        {history.length} jobs visibles
                    </Badge>
                    <Button
                        appearance="primary"
                        icon={loadingPilotTransfers ? <Spinner size="tiny" /> : <PlayRegular />}
                        onClick={() => { void handleRunPilotTransfers(); }}
                        disabled={loadingPilotTransfers}
                    >
                        {loadingPilotTransfers ? 'Encolando transferencias...' : 'Ejecutar transferencias piloto ahora'}
                    </Button>
                    <Button
                        appearance="secondary"
                        icon={purgingCompanyData ? <Spinner size="tiny" /> : <DismissCircleRegular />}
                        onClick={() => { void handlePurgeCompanyData(); }}
                        disabled={purgingCompanyData}
                    >
                        {purgingCompanyData ? 'Borrando...' : 'Borrar jobs y logs de esta empresa'}
                    </Button>
                    <Button
                        appearance="secondary"
                        icon={loadingHistory || loadingRecurring || loadingStats ? <Spinner size="tiny" /> : <ArrowSyncRegular />}
                        onClick={() => {
                            void loadRecentJobs(false);
                            void loadRecurringJobs(false);
                            void loadRecordingConfig(false);
                            void loadStats(false);
                        }}
                    >
                        Actualizar
                    </Button>
                </div>
            </div>

            <div className={styles.summaryGrid}>
                <div className={styles.summaryCard} style={{ borderLeftColor: tokens.colorPaletteGreenBorderActive }}>
                    <div className={styles.jobCardIcon} style={{ backgroundColor: `${tokens.colorPaletteGreenBorderActive}22`, color: tokens.colorPaletteGreenBorderActive }}>
                        <CheckmarkCircleRegular />
                    </div>
                    <div className={styles.summaryContent}>
                        <div className={styles.summaryLabel}>Completados</div>
                        <div className={styles.summaryValue} style={{ color: tokens.colorPaletteGreenForeground1 }}>
                            {stats?.succeeded ?? 0}
                        </div>
                    </div>
                </div>

                <div className={styles.summaryCard} style={{ borderLeftColor: tokens.colorPaletteRedBorderActive }}>
                    <div className={styles.jobCardIcon} style={{ backgroundColor: `${tokens.colorPaletteRedBorderActive}22`, color: tokens.colorPaletteRedBorderActive }}>
                        <DismissCircleRegular />
                    </div>
                    <div className={styles.summaryContent}>
                        <div className={styles.summaryLabel}>Fallidos</div>
                        <div className={styles.summaryValue} style={{ color: tokens.colorPaletteRedForeground1 }}>
                            {stats?.failed ?? 0}
                        </div>
                    </div>
                </div>

                <div className={styles.summaryCard} style={{ borderLeftColor: tokens.colorBrandStroke1 }}>
                    <div className={styles.jobCardIcon} style={{ backgroundColor: `${tokens.colorBrandStroke1}22`, color: tokens.colorBrandStroke1 }}>
                        <Spinner size="tiny" />
                    </div>
                    <div className={styles.summaryContent}>
                        <div className={styles.summaryLabel}>En Proceso</div>
                        <div className={styles.summaryValue} style={{ color: tokens.colorBrandForeground1 }}>
                            {stats?.processing ?? 0}
                        </div>
                    </div>
                </div>

                <div className={styles.summaryCard} style={{ borderLeftColor: tokens.colorPaletteMarigoldBorderActive }}>
                    <div className={styles.jobCardIcon} style={{ backgroundColor: `${tokens.colorPaletteMarigoldBorderActive}22`, color: tokens.colorPaletteMarigoldBorderActive }}>
                        <ClockRegular />
                    </div>
                    <div className={styles.summaryContent}>
                        <div className={styles.summaryLabel}>En Cola</div>
                        <div className={styles.summaryValue} style={{ color: tokens.colorPaletteMarigoldForeground1 }}>
                            {(stats?.enqueued ?? 0) + (stats?.scheduled ?? 0)}
                        </div>
                    </div>
                </div>
            </div>

            <div className={styles.mainCard}>
                <div className={styles.sectionInput}>
                    <div className={styles.inputGroup}>
                        <Label weight="semibold">ID de Seccion</Label>
                        <Input
                            placeholder="Ej. 12345"
                            size="large"
                            value={idSeccion}
                            onChange={(_e, d) => setIdSeccion(d.value)}
                            type="number"
                        />
                    </div>
                    <Tooltip content="Este ID se usa para todas las tareas del panel." relationship="description">
                        <Button appearance="subtle" icon={<InfoRegular />}>
                            Info
                        </Button>
                    </Tooltip>
                </div>

                <div className={styles.sectionInput} style={{ marginTop: '16px' }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', minWidth: '260px' }}>
                        <Label weight="semibold">Job Transferencia Grabaciones</Label>
                        {loadingRecordingConfig ? (
                            <Spinner size="tiny" label="Cargando configuracion..." />
                        ) : recordingConfig ? (
                            <>
                                <Switch
                                    label={recordingConfig.isEnabled ? 'Habilitado' : 'Deshabilitado'}
                                    checked={recordingConfig.isEnabled}
                                    onChange={(_, data) =>
                                        setRecordingConfig(prev => prev ? { ...prev, isEnabled: data.checked } : prev)
                                    }
                                />
                                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                                    TZ: {recordingConfig.timeZoneId} | Pilot mode: {recordingConfig.isPilotMode ? 'On' : 'Off'} | Secciones: {recordingConfig.pilotSectionsConfigured}
                                </Text>
                            </>
                        ) : (
                            <Text size={200} style={{ color: tokens.colorPaletteRedForeground1 }}>
                                No disponible.
                            </Text>
                        )}
                    </div>
                    <div className={styles.inputGroup} style={{ maxWidth: '360px' }}>
                        <Label weight="semibold">CRON</Label>
                        <Input
                            placeholder="Ej. */30 * * * *"
                            size="large"
                            value={recordingCronDraft}
                            onChange={(_, d) => setRecordingCronDraft(d.value)}
                            disabled={loadingRecordingConfig || !recordingConfig}
                        />
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                            Formato 5 campos o alias: @hourly, @daily.
                        </Text>
                    </div>
                    <Button
                        appearance="primary"
                        icon={savingRecordingConfig ? <Spinner size="tiny" /> : <CheckmarkCircleRegular />}
                        onClick={() => { void handleSaveRecordingConfig(); }}
                        disabled={!recordingConfig || loadingRecordingConfig || savingRecordingConfig}
                    >
                        {savingRecordingConfig ? 'Guardando...' : 'Guardar config job'}
                    </Button>
                </div>

                <div className={styles.sectionInput} style={{ marginTop: '16px' }}>
                    <div className={styles.inputGroup}>
                        <Label weight="semibold">Buscar Job ID</Label>
                        <Input
                            placeholder="Ej. 123 o 12345"
                            size="large"
                            value={jobIdLookup}
                            onChange={(_e, d) => setJobIdLookup(d.value)}
                        />
                    </div>
                    <Button
                        appearance="secondary"
                        icon={loadingHistory ? <Spinner size="tiny" /> : <SearchRegular />}
                        onClick={() => { void handleLookupJob(); }}
                        disabled={loadingHistory}
                    >
                        Buscar job
                    </Button>
                    <Button
                        appearance="subtle"
                        onClick={() => {
                            setJobIdLookup('');
                            void loadRecentJobs(false);
                        }}
                        disabled={loadingHistory}
                    >
                        Limpiar
                    </Button>
                </div>

                <Divider style={{ margin: '20px 0' }} />

                <TabList selectedValue={selectedTab} onTabSelect={onTabSelect} size="large">
                    <Tab value="all">Todas</Tab>
                    <Tab value="sync">Sincronizacion</Tab>
                    <Tab value="schedule">Agendas</Tab>
                </TabList>

                <div className={styles.jobCardsGrid}>
                    {filteredJobs.map(job => (
                        <div key={job.key} className={styles.jobCard}>
                            <div className={styles.jobCardHeader}>
                                <div
                                    className={styles.jobCardIcon}
                                    style={{ backgroundColor: `${job.color}22`, color: job.color }}
                                >
                                    {job.icon}
                                </div>
                                <div>
                                    <div className={styles.jobCardTitle}>{job.label}</div>
                                    <Badge
                                        appearance="outline"
                                        size="small"
                                        color={job.category === 'sync' ? 'brand' : 'success'}
                                    >
                                        {job.category === 'sync' ? 'Sincronizacion' : 'Agendas'}
                                    </Badge>
                                </div>
                            </div>
                            <div className={styles.jobCardDescription}>{job.description}</div>
                            <Button
                                appearance="primary"
                                icon={loading === job.key ? <Spinner size="tiny" /> : <PlayRegular />}
                                disabled={!idSeccion || loading !== null}
                                onClick={() => handleEnqueueJob(job)}
                                style={{ alignSelf: 'flex-start' }}
                            >
                                {loading === job.key ? 'Encolando...' : 'Ejecutar'}
                            </Button>
                        </div>
                    ))}
                </div>
            </div>

            <div className={styles.historyContainer}>
                <div className={styles.sectionHeader}>
                    <Title3>Jobs Recurrentes</Title3>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                        Registro, ultima ejecucion y siguiente disparo
                    </Text>
                </div>
                <Divider />

                {loadingRecurring && recurringJobs.length === 0 ? (
                    <div className={styles.emptyState}>
                        <Spinner label="Cargando jobs recurrentes..." />
                    </div>
                ) : recurringJobs.length === 0 ? (
                    <div className={styles.emptyState}>
                        <ClockRegular style={{ fontSize: '48px', marginBottom: '12px', opacity: 0.4 }} />
                        <div style={{ fontWeight: 600, marginBottom: '4px' }}>Sin jobs recurrentes</div>
                        <div style={{ fontSize: '13px' }}>Este tenant no tiene recurring jobs registrados.</div>
                    </div>
                ) : (
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>Job / Empresa</TableHeaderCell>
                                <TableHeaderCell>Metodo</TableHeaderCell>
                                <TableHeaderCell>Seccion</TableHeaderCell>
                                <TableHeaderCell>Agregado</TableHeaderCell>
                                <TableHeaderCell>Ultima ejecucion</TableHeaderCell>
                                <TableHeaderCell>Resultado ultimo run</TableHeaderCell>
                                <TableHeaderCell>Siguiente</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {recurringJobs.map((entry) => (
                                <TableRow key={entry.id}>
                                    <TableCell>
                                        <div style={{ fontWeight: 600 }}>{entry.id}</div>
                                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', marginTop: '4px' }}>
                                            {entry.companyKey && (
                                                <Badge size="small" appearance="filled" style={{ backgroundColor: tokens.colorBrandBackground2, color: tokens.colorBrandForeground2, fontWeight: 'bold' }}>
                                                    {entry.companyKey}
                                                </Badge>
                                            )}
                                            <Badge appearance="outline" color={entry.removed ? 'danger' : 'brand'}>
                                                {entry.removed ? 'Removed' : entry.queue || 'default'}
                                            </Badge>
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div style={{ wordBreak: 'break-all', whiteSpace: 'normal', maxWidth: '200px' }}>{entry.method}</div>
                                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                                            {entry.cron}
                                        </Text>
                                    </TableCell>
                                    <TableCell>
                                        <Badge appearance="outline">{entry.sectionCode || entry.idSeccion || '-'}</Badge>
                                    </TableCell>
                                    <TableCell>{formatDateTime(entry.createdAt)}</TableCell>
                                    <TableCell>
                                        <div>{formatDateTime(entry.lastExecution)}</div>
                                        <div style={{ marginTop: '4px' }}>
                                            <Badge appearance="outline" size="small" color={getStateBadgeColor(entry.lastJobState || 'informative')}>
                                                {entry.lastJobState || 'Sin ejecutar'}
                                            </Badge>
                                        </div>
                                        {entry.lastJobId ? (
                                            <div className={styles.mono} style={{ marginTop: '4px', color: tokens.colorNeutralForeground3 }}>
                                                {entry.lastJobId}
                                            </div>
                                        ) : null}
                                    </TableCell>
                                    <TableCell>
                                        <Text size={200} style={{ color: entry.error ? tokens.colorPaletteRedForeground1 : undefined }}>
                                            {entry.error || entry.lastResult || '-'}
                                        </Text>
                                    </TableCell>
                                    <TableCell>
                                        <div>{formatDateTime(entry.nextExecution)}</div>
                                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                                            {entry.timeZoneId || '-'}
                                        </Text>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                )}
            </div>

            <div className={styles.historyContainer}>
                <div className={styles.sectionHeader}>
                    <Title3>Historial de Ejecucion (Hangfire)</Title3>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                        Refresco automatico cada 3 min
                    </Text>
                </div>
                <Divider />

                {history.length === 0 ? (
                    <div className={styles.emptyState}>
                        <ClockRegular style={{ fontSize: '48px', marginBottom: '12px', opacity: 0.4 }} />
                        <div style={{ fontWeight: 600, marginBottom: '4px' }}>Sin ejecuciones visibles</div>
                        <div style={{ fontSize: '13px' }}>No hay jobs recientes para este tenant.</div>
                    </div>
                ) : (
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>Estado</TableHeaderCell>
                                <TableHeaderCell>Metodo / Empresa</TableHeaderCell>
                                <TableHeaderCell>Seccion</TableHeaderCell>
                                <TableHeaderCell>Ejecutado por</TableHeaderCell>
                                <TableHeaderCell>Job ID</TableHeaderCell>
                                <TableHeaderCell>Mensaje</TableHeaderCell>
                                <TableHeaderCell>Hora</TableHeaderCell>
                                <TableHeaderCell>Accion</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {history.map((entry) => (
                                <TableRow key={entry.jobId}>
                                    <TableCell>{getStateIcon(entry.state)}</TableCell>
                                    <TableCell style={{ fontWeight: 600 }}>
                                        <div style={{ wordBreak: 'break-all', whiteSpace: 'normal', maxWidth: '200px' }}>{entry.method}</div>
                                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', marginTop: '4px', flexWrap: 'wrap' }}>
                                            {entry.companyKey && (
                                                <Badge size="small" appearance="filled" style={{ backgroundColor: tokens.colorBrandBackground2, color: tokens.colorBrandForeground2, fontWeight: 'bold' }}>
                                                    {entry.companyKey}
                                                </Badge>
                                            )}
                                            <Badge appearance="outline" size="small" color={getStateBadgeColor(entry.state)}>
                                                {entry.state}
                                            </Badge>
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <Badge appearance="outline">{entry.sectionCode || entry.idSeccion || '-'}</Badge>
                                    </TableCell>
                                    <TableCell>
                                        {(() => {
                                            const args = entry.arguments || [];
                                            const lastArg = args.length ? args[args.length - 1] : null;
                                            
                                            // Normalizar para comparación
                                            const normalizedArg = lastArg ? lastArg.toString().replace(/"/g, '').trim().toLowerCase() : '';
                                            const normalizedCompany = entry.companyKey ? entry.companyKey.toLowerCase() : '';
                                            
                                            // El argumento no es un usuario si:
                                            // - Es nulo o vacío o "null"
                                            // - Es igual al companyKey
                                            // - Es un número (probablemente un ID de sección)
                                            // - Solo hay 1 argumento y el método es de los que siempre llevan companyKey primero
                                            const isSystem = !normalizedArg || 
                                                           normalizedArg === 'null' || 
                                                           normalizedArg === normalizedCompany || 
                                                           !isNaN(Number(normalizedArg));
                                            
                                            const displayName = isSystem ? 'Sistema' : (lastArg?.toString().replace(/"/g, '') || 'Sistema');

                                            return (
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                    <PersonRegular style={{ fontSize: '14px', color: tokens.colorNeutralForeground3 }} />
                                                    <Text size={200} weight={!isSystem ? "semibold" : "regular"}>
                                                        {displayName}
                                                    </Text>
                                                </div>
                                            );
                                        })()}
                                    </TableCell>
                                    <TableCell className={styles.mono}>
                                        {entry.jobId}
                                    </TableCell>
                                    <TableCell>
                                        <Text
                                            size={200}
                                            style={{
                                                color: entry.error
                                                    ? (entry.state.toLowerCase().includes('succeed')
                                                        ? tokens.colorPaletteGreenForeground1
                                                        : tokens.colorPaletteRedForeground1)
                                                    : undefined
                                            }}
                                        >
                                            {entry.error ? entry.error : (entry.arguments?.length ? `Parámetros: ${entry.arguments.join(', ')}` : '-')}
                                        </Text>
                                    </TableCell>
                                    <TableCell style={{ fontSize: '12px', color: tokens.colorNeutralForeground3 }}>
                                        {formatDateTime(entry.timestamp)}
                                    </TableCell>
                                    <TableCell>
                                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
                                            {canRequeueJob(entry.state) && (
                                                <Button
                                                    appearance="secondary"
                                                    size="small"
                                                    icon={requeueingJobId === entry.jobId ? <Spinner size="tiny" /> : <ArrowSyncRegular />}
                                                    onClick={() => { void handleRequeueJob(entry.jobId); }}
                                                    disabled={requeueingJobId !== null || cancellingJobId !== null}
                                                >
                                                    {requeueingJobId === entry.jobId ? 'Re-encolando...' : 'Requeue'}
                                                </Button>
                                            )}
                                            {canCancelJob(entry.state) && (
                                                <Button
                                                    appearance="transparent"
                                                    size="small"
                                                    style={{ color: tokens.colorPaletteRedForeground1, border: `1px solid ${tokens.colorPaletteRedBorder1}` }}
                                                    icon={cancellingJobId === entry.jobId ? <Spinner size="tiny" /> : <DismissCircleRegular />}
                                                    onClick={() => { void handleCancelJob(entry.jobId); }}
                                                    disabled={cancellingJobId !== null || requeueingJobId !== null}
                                                >
                                                    {cancellingJobId === entry.jobId ? 'Deteniendo...' : 'Detener'}
                                                </Button>
                                            )}
                                            {!canRequeueJob(entry.state) && !canCancelJob(entry.state) && (
                                                <Badge appearance="outline" size="small" color="subtle">
                                                    <DismissCircleRegular />
                                                    No aplica
                                                </Badge>
                                            )}
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                )}
            </div>
        </div>
    );
};

export default JobsPage;
