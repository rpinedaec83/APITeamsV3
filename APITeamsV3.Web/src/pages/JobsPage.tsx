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
} from '@fluentui/react-components';
import {
    PlayRegular,
    ArrowSyncRegular,
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
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';
import { showWarning } from '../utils/alerts';

interface HangfireJobResult {
    jobId: string;
    state: string;
    method: string;
    idSeccion?: number | null;
    timestamp: string;
    arguments: string[];
    error?: string;
}

interface HangfireRecurringJobResult {
    id: string;
    cron: string;
    queue: string;
    method: string;
    idSeccion?: number | null;
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
    const [history, setHistory] = useState<HangfireJobResult[]>([]);
    const [recurringJobs, setRecurringJobs] = useState<HangfireRecurringJobResult[]>([]);
    const [selectedTab, setSelectedTab] = useState<TabValue>('all');

    const loadRecentJobs = async (silent = false) => {
        if (!silent) setLoadingHistory(true);
        try {
            const response = await apiClient.get('/jobs/recent', { params: { take: 100 } });
            const jobs = Array.isArray(response.data) ? response.data as HangfireJobResult[] : [];
            setHistory(jobs);
        } catch (err: unknown) {
            if (!silent) {
                const errorMessage = err instanceof Error ? err.message : 'No se pudo cargar historial de Hangfire.';
                showWarning(errorMessage);
            }
        } finally {
            if (!silent) setLoadingHistory(false);
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
                const errorMessage = err instanceof Error ? err.message : 'No se pudieron cargar los jobs recurrentes.';
                showWarning(errorMessage);
            }
        } finally {
            if (!silent) setLoadingRecurring(false);
        }
    };

    useEffect(() => {
        void loadRecentJobs(false);
        void loadRecurringJobs(false);
        const interval = window.setInterval(() => {
            void loadRecentJobs(true);
            void loadRecurringJobs(true);
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
            const errorMessage = err instanceof Error ? err.message : 'Error desconocido';
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

    const formatDateTime = (value?: string | null) => {
        if (!value) return '-';
        return new Date(value).toLocaleString('es-PE');
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
                        appearance="secondary"
                        icon={loadingHistory || loadingRecurring ? <Spinner size="tiny" /> : <ArrowSyncRegular />}
                        onClick={() => {
                            void loadRecentJobs(false);
                            void loadRecurringJobs(false);
                        }}
                    >
                        Actualizar
                    </Button>
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
                                <TableHeaderCell>Job</TableHeaderCell>
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
                                        <div style={{ marginTop: '4px' }}>
                                            <Badge appearance="outline" color={entry.removed ? 'danger' : 'brand'}>
                                                {entry.removed ? 'Removed' : entry.queue || 'default'}
                                            </Badge>
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        <div>{entry.method}</div>
                                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                                            {entry.cron}
                                        </Text>
                                    </TableCell>
                                    <TableCell>
                                        <Badge appearance="outline">{entry.idSeccion ?? '-'}</Badge>
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
                        Refresco automatico cada 15s
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
                                <TableHeaderCell>Metodo</TableHeaderCell>
                                <TableHeaderCell>Seccion</TableHeaderCell>
                                <TableHeaderCell>Job ID</TableHeaderCell>
                                <TableHeaderCell>Mensaje</TableHeaderCell>
                                <TableHeaderCell>Hora</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {history.map((entry) => (
                                <TableRow key={entry.jobId}>
                                    <TableCell>{getStateIcon(entry.state)}</TableCell>
                                    <TableCell style={{ fontWeight: 600 }}>
                                        <div>{entry.method}</div>
                                        <Badge appearance="outline" size="small" color={getStateBadgeColor(entry.state)}>
                                            {entry.state}
                                        </Badge>
                                    </TableCell>
                                    <TableCell>
                                        <Badge appearance="outline">{entry.idSeccion ?? '-'}</Badge>
                                    </TableCell>
                                    <TableCell className={styles.mono}>
                                        {entry.jobId}
                                    </TableCell>
                                    <TableCell>
                                        <Text size={200} style={{ color: entry.error ? tokens.colorPaletteRedForeground1 : undefined }}>
                                            {entry.error || (entry.arguments?.length ? entry.arguments.join(' | ') : '-')}
                                        </Text>
                                    </TableCell>
                                    <TableCell style={{ fontSize: '12px', color: tokens.colorNeutralForeground3 }}>
                                        {new Date(entry.timestamp).toLocaleString('es-PE')}
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
