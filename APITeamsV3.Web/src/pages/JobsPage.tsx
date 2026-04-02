import type { SelectTabData, TabValue } from '@fluentui/react-components';
import React, { useState } from 'react';
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

// Removed hardcoded API_BASE

interface JobResult {
    jobId: string;
    message: string;
    timestamp: Date;
    jobType: string;
    idSeccion: number;
    status: 'success' | 'error';
    error?: string;
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
    emptyState: {
        textAlign: 'center' as const,
        padding: '40px 20px',
        color: tokens.colorNeutralForeground3,
    },
    categoryLabel: {
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
        textTransform: 'uppercase' as const,
        letterSpacing: '0.05em',
        marginTop: '8px',
    },
});

interface JobDefinition {
    key: string;
    label: string;
    description: string;
    endpoint: string;
    icon: React.ReactNode;
    color: string;
    category: 'sync' | 'provisioning' | 'schedule';
}

const JOB_DEFINITIONS: JobDefinition[] = [
    // Sync Jobs
    {
        key: 'sync-missing-students',
        label: 'Sync Alumnos Faltantes',
        description: 'Detecta alumnos matriculados en Smart que aún no están en el equipo de Teams.',
        endpoint: '/api/jobs/sync-missing-students',
        icon: <PeopleRegular />,
        color: tokens.colorPaletteBlueBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-obsolete-students',
        label: 'Sync Alumnos Obsoletos',
        description: 'Detecta alumnos que están en Teams pero ya no están matriculados en Smart.',
        endpoint: '/api/jobs/sync-obsolete-students',
        icon: <PersonDeleteRegular />,
        color: tokens.colorPaletteRedBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-renamed-teams',
        label: 'Sync Equipos Renombrados',
        description: 'Detecta equipos cuyo nombre o descripción no coinciden con la política de nomenclatura Smart.',
        endpoint: '/api/jobs/sync-renamed-teams',
        icon: <RenameRegular />,
        color: tokens.colorPaletteMarigoldBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-facilitator',
        label: 'Sync Facilitador',
        description: 'Sincroniza el facilitador asignado en Smart con el propietario del equipo en Teams.',
        endpoint: '/api/jobs/sync-facilitator',
        icon: <PersonRegular />,
        color: tokens.colorPalettePurpleBorderActive,
        category: 'sync',
    },
    {
        key: 'sync-roster',
        label: 'Sync Roster Completo',
        description: 'Ejecuta una sincronización completa del roster de sesiones (altas y bajas).',
        endpoint: '/api/jobs/sync-roster',
        icon: <ArrowSyncRegular />,
        color: tokens.colorPaletteTealBorderActive,
        category: 'sync',
    },
    // Schedule Jobs
    {
        key: 'generate-schedule',
        label: 'Generar Agendas',
        description: 'Genera las agendas de sesiones de Teams para la sección indicada.',
        endpoint: '/api/jobs/generate-schedule',
        icon: <CalendarRegular />,
        color: tokens.colorPaletteGreenBorderActive,
        category: 'schedule',
    },
    {
        key: 'sync-dates',
        label: 'Sync Fechas',
        description: 'Sincroniza las fechas de las sesiones de Teams con las de Smart.',
        endpoint: '/api/jobs/sync-dates',
        icon: <CalendarSyncRegular />,
        color: tokens.colorPaletteBerryBorderActive,
        category: 'schedule',
    },
];

const JobsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    const [idSeccion, setIdSeccion] = useState('');
    const [loading, setLoading] = useState<string | null>(null); // tracks which job key is loading
    const [history, setHistory] = useState<JobResult[]>([]);
    const [selectedTab, setSelectedTab] = useState<TabValue>('all');

    const handleEnqueueJob = async (job: JobDefinition) => {
        if (!idSeccion || !idSeccion.trim()) {
            showWarning('Ingrese un ID de Sección válido');
            return;
        }

        const seccionId = parseInt(idSeccion, 10);
        if (isNaN(seccionId)) {
            showWarning('El ID de Sección debe ser numérico');
            return;
        }

        setLoading(job.key);
        try {
            const url = job.key === 'sync-roster'
                ? `${job.endpoint}/${seccionId}?fullSync=true`
                : `${job.endpoint}/${seccionId}`;

            const response = await apiClient.post(url, {}, {
                headers: {
                    'X-Tenant': 'idat', // Should this be dynamic? Keeping it for now.
                },
            });

            const result = response.data;
            setHistory(prev => [{
                jobId: result.jobId,
                message: result.message,
                timestamp: new Date(),
                jobType: job.label,
                idSeccion: seccionId,
                status: 'success',
            }, ...prev]);

        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error desconocido';
            setHistory(prev => [{
                jobId: '-',
                message: errorMessage,
                timestamp: new Date(),
                jobType: job.label,
                idSeccion: seccionId,
                status: 'error',
                error: errorMessage,
            }, ...prev]);
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

    return (
        <div className={styles.root}>
            {/* Header */}
            <div className={styles.header}>
                <div className={styles.headerTitle}>
                    <Avatar color="brand" icon={<TimerRegular />} size={48} />
                    <div>
                        <Title3>Hangfire Jobs</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>
                            Configura y ejecuta tareas de sincronización de Teams
                        </div>
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <Badge appearance="filled" color="informative" size="large">
                        {history.length} jobs ejecutados
                    </Badge>
                </div>
            </div>

            {/* Section Input */}
            <div className={styles.mainCard}>
                <div className={styles.sectionInput}>
                    <div className={styles.inputGroup}>
                        <Label weight="semibold">ID de Sección</Label>
                        <Input
                            placeholder="Ej. 12345"
                            size="large"
                            value={idSeccion}
                            onChange={(_e, d) => setIdSeccion(d.value)}
                            type="number"
                            onKeyDown={(e) => {
                                if (e.key === 'Enter') {
                                    // Focus on first job card button
                                }
                            }}
                        />
                    </div>
                    <Tooltip content="Este ID se usará para todas las tareas" relationship="description">
                        <Button appearance="subtle" icon={<InfoRegular />}>
                            Info
                        </Button>
                    </Tooltip>
                </div>

                <Divider style={{ margin: '20px 0' }} />

                {/* Category Tabs */}
                <TabList selectedValue={selectedTab} onTabSelect={onTabSelect} size="large">
                    <Tab value="all">Todas</Tab>
                    <Tab value="sync">Sincronización</Tab>
                    <Tab value="schedule">Agendas</Tab>
                </TabList>

                {/* Job Cards Grid */}
                <div className={styles.jobCardsGrid}>
                    {filteredJobs.map(job => (
                        <div key={job.key} className={styles.jobCard}>
                            <div className={styles.jobCardHeader}>
                                <div
                                    className={styles.jobCardIcon}
                                    style={{ backgroundColor: job.color + '22', color: job.color }}
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
                                        {job.category === 'sync' ? 'Sincronización' : 'Agendas'}
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

            {/* Job History */}
            <div className={styles.historyContainer}>
                <div style={{ padding: '16px 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <Title3>Historial de Ejecución</Title3>
                    {history.length > 0 && (
                        <Button appearance="subtle" size="small" onClick={() => setHistory([])}>
                            Limpiar
                        </Button>
                    )}
                </div>
                <Divider />

                {history.length === 0 ? (
                    <div className={styles.emptyState}>
                        <ClockRegular style={{ fontSize: '48px', marginBottom: '12px', opacity: 0.4 }} />
                        <div style={{ fontWeight: 600, marginBottom: '4px' }}>Sin ejecuciones</div>
                        <div style={{ fontSize: '13px' }}>Seleccione una sección y ejecute una tarea para ver los resultados aquí.</div>
                    </div>
                ) : (
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>Estado</TableHeaderCell>
                                <TableHeaderCell>Tarea</TableHeaderCell>
                                <TableHeaderCell>Sección</TableHeaderCell>
                                <TableHeaderCell>Job ID</TableHeaderCell>
                                <TableHeaderCell>Mensaje</TableHeaderCell>
                                <TableHeaderCell>Hora</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {history.map((entry, index) => (
                                <TableRow key={index}>
                                    <TableCell>
                                        {entry.status === 'success' ? (
                                            <CheckmarkCircleRegular style={{ color: tokens.colorPaletteGreenForeground1, fontSize: '20px' }} />
                                        ) : (
                                            <ErrorCircleRegular style={{ color: tokens.colorPaletteRedForeground1, fontSize: '20px' }} />
                                        )}
                                    </TableCell>
                                    <TableCell style={{ fontWeight: 600 }}>{entry.jobType}</TableCell>
                                    <TableCell>
                                        <Badge appearance="outline">{entry.idSeccion}</Badge>
                                    </TableCell>
                                    <TableCell style={{ fontFamily: 'monospace', fontSize: '12px' }}>
                                        {entry.jobId}
                                    </TableCell>
                                    <TableCell>
                                        <Text size={200} style={{ color: entry.status === 'error' ? tokens.colorPaletteRedForeground1 : undefined }}>
                                            {entry.message}
                                        </Text>
                                    </TableCell>
                                    <TableCell style={{ fontSize: '12px', color: tokens.colorNeutralForeground3 }}>
                                        {entry.timestamp.toLocaleTimeString()}
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
