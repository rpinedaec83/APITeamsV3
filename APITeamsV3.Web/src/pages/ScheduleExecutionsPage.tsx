import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useApiClient } from '../hooks/useApiClient';
import {
    Badge,
    Button,
    Card,
    Spinner,
    Text,
    Title1,
    Title3,
    makeStyles,
    shorthands,
    tokens,
} from '@fluentui/react-components';
import { ArrowLeftRegular, ClockRegular, CheckmarkCircleRegular, ErrorCircleRegular } from '@fluentui/react-icons';

interface SyncScheduleExecution {
    id: number;
    status: string;
    triggerSource: string;
    sedeCodes: string;
    totalSections: number;
    enqueuedJobsCount: number;
    jobIds: string[];
    errorMessage: string;
    triggeredAtUtc: string;
    completedAtUtc?: string | null;
}

interface SyncScheduleDetails {
    id: number;
    companyConfigId: number;
    companyName: string;
    timeZoneId: string;
    daysOfWeek: string;
    hour: number;
    minute: number;
    isEnabled: boolean;
    createdAt: string;
    lastRunAt?: string | null;
    executions: SyncScheduleExecution[];
}

const WINDOWS_TO_IANA_TIMEZONES: Record<string, string> = {
    'SA Pacific Standard Time': 'America/Lima',
    'Pacific SA Standard Time': 'America/Santiago',
    UTC: 'UTC',
};

const useStyles = makeStyles({
    root: {
        minHeight: '100%',
        padding: '28px',
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        background: 'linear-gradient(180deg, #eef2f7 0%, #e6ecf5 100%)',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '12px',
        backgroundColor: '#fff',
        ...shorthands.borderRadius('20px'),
        ...shorthands.padding('20px'),
        boxShadow: tokens.shadow8,
    },
    card: {
        backgroundColor: '#fff',
        ...shorthands.borderRadius('20px'),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        boxShadow: tokens.shadow8,
        overflow: 'hidden',
    },
    cardBody: {
        ...shorthands.padding('20px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '18px',
    },
    metrics: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
        gap: '12px',
        '@media (max-width: 900px)': {
            gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        },
        '@media (max-width: 640px)': {
            gridTemplateColumns: '1fr',
        },
    },
    metricCard: {
        backgroundColor: '#f7f9fc',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.borderRadius('14px'),
        ...shorthands.padding('14px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    executionList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
    },
    executionCard: {
        backgroundColor: '#fbfcfe',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.borderRadius('16px'),
        ...shorthands.padding('16px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    executionHead: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: '12px',
        alignItems: 'flex-start',
    },
    mono: {
        fontFamily: 'monospace',
        fontSize: '12px',
    },
    flexColumn: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
    },
});

const resolveBrowserTimeZone = (timeZoneId?: string) => WINDOWS_TO_IANA_TIMEZONES[timeZoneId ?? ''] ?? timeZoneId ?? 'America/Lima';
const formatTime = (hour: number, minute: number) => `${hour.toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`;
const formatDateTime = (value: string | null | undefined, timeZoneId?: string) => {
    if (!value) return '—';
    const date = new Date(/z$|[+-]\d{2}:\d{2}$/i.test(value) ? value : `${value}Z`);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('es-PE', {
        timeZone: resolveBrowserTimeZone(timeZoneId),
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
    }).format(date);
};

const ScheduleExecutionsPage: React.FC = () => {
    const styles = useStyles();
    const api = useApiClient();
    const navigate = useNavigate();
    const { id } = useParams<{ id: string }>();
    const [loading, setLoading] = useState(true);
    const [details, setDetails] = useState<SyncScheduleDetails | null>(null);

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            try {
                const response = await api.get(`/admin/sync-schedules/${id}/details`);
                setDetails(response.data as SyncScheduleDetails);
            } catch (error) {
                console.error(error);
                setDetails(null);
            } finally {
                setLoading(false);
            }
        };

        void load();
    }, [api, id]);

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div className={styles.flexColumn}>
                    <Title1 style={{ margin: 0 }}>Detalle de ejecuciones</Title1>
                    <Text size={200} weight="regular" style={{ color: tokens.colorNeutralForeground2 }}>
                        Revisa el historial de disparos, resultados y tareas encoladas por la programación.
                    </Text>
                </div>
                <Button appearance="secondary" icon={<ArrowLeftRegular />} onClick={() => navigate('/schedules')}>
                    Volver a programaciones
                </Button>
            </div>

            {loading ? (
                <Card className={styles.card}>
                    <div className={styles.cardBody}>
                        <Spinner label="Cargando detalle de la programación..." />
                    </div>
                </Card>
            ) : !details ? (
                <Card className={styles.card}>
                    <div className={styles.cardBody}>
                        <Title3>No se encontró la programación</Title3>
                    </div>
                </Card>
            ) : (
                <>
                    <Card className={styles.card}>
                        <div className={styles.cardBody}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', gap: '12px', alignItems: 'flex-start', flexWrap: 'wrap' }}>
                                <div className={styles.flexColumn}>
                                    <Title3 style={{ margin: 0 }}>{details.companyName}</Title3>
                                    <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>Programación #{details.id} · {details.timeZoneId}</Text>
                                </div>
                                <Badge appearance="filled" color={details.isEnabled ? 'success' : 'danger'}>
                                    {details.isEnabled ? 'Activo' : 'Inactivo'}
                                </Badge>
                            </div>

                            <div className={styles.metrics}>
                                <div className={styles.metricCard}>
                                    <Text size={200} weight="semibold">Frecuencia</Text>
                                    <Text>{details.daysOfWeek}</Text>
                                </div>
                                <div className={styles.metricCard}>
                                    <Text size={200} weight="semibold">Hora</Text>
                                    <Text size={600} weight="bold">{formatTime(details.hour, details.minute)}</Text>
                                </div>
                                <div className={styles.metricCard}>
                                    <Text size={200} weight="semibold">Creado</Text>
                                    <Text>{formatDateTime(details.createdAt, details.timeZoneId)}</Text>
                                </div>
                                <div className={styles.metricCard}>
                                    <Text size={200} weight="semibold">Último run</Text>
                                    <Text>{formatDateTime(details.lastRunAt, details.timeZoneId)}</Text>
                                </div>
                            </div>
                        </div>
                    </Card>

                    <Card className={styles.card}>
                        <div className={styles.cardBody}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px' }}>
                                <Title3 style={{ margin: 0 }}>Historial de ejecuciones</Title3>
                                <Badge appearance="outline">{details.executions.length} run(s)</Badge>
                            </div>

                            {details.executions.length === 0 ? (
                                <Text>No hay ejecuciones registradas todavía.</Text>
                            ) : (
                                <div className={styles.executionList}>
                                    {details.executions.map(execution => (
                                        <div key={execution.id} className={styles.executionCard}>
                                            <div className={styles.executionHead}>
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
                                                    {execution.status === 'Succeeded'
                                                        ? <CheckmarkCircleRegular color={tokens.colorPaletteGreenForeground1} />
                                                        : execution.status === 'Failed'
                                                            ? <ErrorCircleRegular color={tokens.colorPaletteRedForeground1} />
                                                            : <ClockRegular color={tokens.colorNeutralForeground3} />}
                                                    <Text weight="semibold">Ejecución #{execution.id}</Text>
                                                    <Badge appearance="filled" color={execution.status === 'Succeeded' ? 'success' : execution.status === 'Failed' ? 'danger' : 'warning'}>
                                                        {execution.status === 'Succeeded' ? 'Completado' : execution.status === 'Failed' ? 'Fallido' : execution.status}
                                                    </Badge>
                                                </div>
                                                <Text size={200}>{formatDateTime(execution.triggeredAtUtc, details.timeZoneId)}</Text>
                                            </div>

                                            <div className={styles.metrics}>
                                                <div className={styles.metricCard}>
                                                    <Text size={200} weight="semibold">Trigger</Text>
                                                    <Text>{execution.triggerSource}</Text>
                                                </div>
                                                <div className={styles.metricCard}>
                                                    <Text size={200} weight="semibold">Sedes</Text>
                                                    <Text>{execution.sedeCodes || '—'}</Text>
                                                </div>
                                                <div className={styles.metricCard}>
                                                    <Text size={200} weight="semibold">Secciones</Text>
                                                    <Text>{execution.totalSections}</Text>
                                                </div>
                                                <div className={styles.metricCard}>
                                                    <Text size={200} weight="semibold">Jobs encolados</Text>
                                                    <Text>{execution.enqueuedJobsCount}</Text>
                                                </div>
                                            </div>

                                            <div>
                                                <Text size={200} weight="semibold">Completado</Text>
                                                <Text>{formatDateTime(execution.completedAtUtc, details.timeZoneId)}</Text>
                                            </div>

                                            {execution.errorMessage ? (
                                                <div>
                                                    <Text size={200} weight="semibold">Error</Text>
                                                    <Text>{execution.errorMessage}</Text>
                                                </div>
                                            ) : null}

                                            {execution.jobIds.length > 0 ? (
                                                <div>
                                                    <Text size={200} weight="semibold">Job IDs</Text>
                                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', marginTop: '6px' }}>
                                                        {execution.jobIds.map(jobId => (
                                                            <div key={jobId} className={styles.mono}>{jobId}</div>
                                                        ))}
                                                    </div>
                                                </div>
                                            ) : null}
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    </Card>
                </>
            )}
        </div>
    );
};

export default ScheduleExecutionsPage;
