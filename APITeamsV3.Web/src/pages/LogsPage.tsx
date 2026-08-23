import React, { startTransition, useDeferredValue, useEffect, useMemo, useState } from 'react';
import {
    Table,
    TableHeader,
    TableRow,
    TableHeaderCell,
    TableBody,
    TableCell,
    Button,
    Title3,
    makeStyles,
    shorthands,
    tokens,
    Badge,
    Select,
    Input,
    Spinner,
    TabList,
    Tab,
    type TabValue,
    Card,
    CardHeader,
    Text,
    Subtitle2,
    Caption1,
    Dialog,
    DialogSurface,
    DialogTitle,
    DialogBody,
    DialogActions,
    DialogTrigger,
} from '@fluentui/react-components';
import {
    OpenRegular,
    PeopleCommunityRegular,
    CalendarMonthRegular,
    GroupRegular,
    ErrorCircleRegular,
    WarningRegular,
    ArrowClockwiseRegular,
    DismissRegular,
    DocumentErrorRegular,
    SearchRegular,
    DraftsRegular,
    CodeRegular,
    PersonRegular,
    WrenchRegular,
    CheckmarkCircleFilled,
    ErrorCircleFilled,
    WarningFilled,
    InfoFilled,
    DeleteRegular
} from '@fluentui/react-icons';
import Swal from 'sweetalert2';
import { useMsal } from '@azure/msal-react';
import { useApiClient } from '../hooks/useApiClient';

interface TeamsLogOperativo {
    id: number;
    companyKey?: string;
    tipo: string;
    entidadAfectada: string;
    referencia: string;
    mensaje: string;
    severidad: string;
    jobId: string;
    usuario: string;
    fecha: string;
    contextoTecnico?: string;
}

interface LogOperationalSummaryDto {
    studentsSuccess: number;
    agendasSuccess: number;
    teamsSuccess: number;
    totalErrors: number;
    totalWarnings: number;
}

interface CompanyOption {
    companyKey: string;
    displayName: string;
}

interface TenantConfigResponse {
    timeZoneId?: string;
}

const PAGE_SIZE = 50;

const WINDOWS_TO_IANA_TIMEZONES: Record<string, string> = {
    'SA Pacific Standard Time': 'America/Lima',
    'Pacific SA Standard Time': 'America/Santiago',
    'Eastern Standard Time': 'America/New_York',
    'SA Western Standard Time': 'America/La_Paz',
    'UTC': 'UTC',
};

const useStyles = makeStyles({
    root: {
        padding: '32px',
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        maxWidth: '1520px',
        margin: '0 auto',
        minHeight: '100vh',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow8,
        borderBottom: `2px solid ${tokens.colorBrandStroke1}`,
    },
    headerIcon: {
        backgroundColor: tokens.colorBrandBackground2,
        ...shorthands.padding('12px'),
        ...shorthands.borderRadius('50%'),
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
    },
    viewSelector: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('8px', '16px'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        boxShadow: tokens.shadow4,
        alignSelf: 'center',
        display: 'flex',
        gap: '8px',
    },
    filterRegion: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: '16px',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        boxShadow: tokens.shadow4,
    },
    filterField: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        "& label": {
            fontSize: '12px',
            fontWeight: tokens.fontWeightSemibold,
            color: tokens.colorNeutralForeground2,
        }
    },
    filterActions: {
        display: 'flex',
        gap: '12px',
        alignItems: 'end',
        justifyContent: 'flex-end',
        gridColumn: '1 / -1',
        marginTop: '8px',
    },
    summaryContainer: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
        gap: '20px',
    },
    summaryCard: {
        ...shorthands.padding('20px'),
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow8,
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        position: 'relative',
        overflow: 'hidden',
        borderBottom: '4px solid transparent',
        transition: 'all 0.3s ease',
        ':hover': {
            boxShadow: tokens.shadow16,
            transform: 'translateY(-4px)',
        }
    },
    summaryIcon: {
        fontSize: '32px',
        opacity: 0.8,
    },
    summaryValue: {
        fontSize: '28px',
        fontWeight: tokens.fontWeightBold,
        lineHeight: '1',
    },
    summaryLabel: {
        fontSize: '14px',
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
    },
    summaryDecoration: {
        position: 'absolute',
        top: '-10px',
        right: '-10px',
        fontSize: '80px',
        opacity: 0.05,
        transform: 'rotate(15deg)',
        pointerEvents: 'none',
    },
    cardStudents: { borderBottomColor: tokens.colorPaletteBlueBorderActive },
    cardAgendas: { borderBottomColor: tokens.colorPalettePurpleBorderActive },
    cardTeams: { borderBottomColor: tokens.colorPaletteTealBorderActive },
    cardErrors: { borderBottomColor: tokens.colorPaletteRedBorderActive },
    cardWarnings: { borderBottomColor: tokens.colorPaletteMarigoldBorderActive },
    contentArea: {
        flexGrow: 1,
    },
    emptyState: {
        textAlign: 'center',
        ...shorthands.padding('100px'),
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow4,
    },
    friendlyGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(400px, 1fr))',
        gap: '20px',
    },
    logCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        transition: 'transform 0.2s, box-shadow 0.2s',
        ':hover': {
            boxShadow: tokens.shadow16,
            transform: 'translateY(-2px)',
        },
        borderLeftWidth: '6px',
        borderLeftStyle: 'solid',
    },
    cardAction: {
        marginTop: '12px',
        ...shorthands.padding('10px'),
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        color: tokens.colorNeutralForeground1,
        fontSize: '13px',
    },
    technicalContainer: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
        boxShadow: tokens.shadow2,
    },
    tableScroll: {
        overflowX: 'auto',
    },
    footer: {
        padding: '20px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    mono: {
        fontFamily: 'Consolas, monospace',
        fontSize: '11px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-all',
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '12px',
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        maxHeight: '400px',
        overflowY: 'auto',
    },
    pager: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
    },
});

const formatDateParam = (dateValue: string): string => {
    if (!dateValue) return '';
    return new Date(`${dateValue}T00:00:00`).toISOString();
};

const resolveBrowserTimeZone = (timeZoneId?: string): string => {
    if (!timeZoneId) return 'America/Lima';
    return WINDOWS_TO_IANA_TIMEZONES[timeZoneId] ?? timeZoneId;
};

const parseUtcDate = (value: string): Date => {
    if (!value) return new Date(NaN);
    const normalized = /z$|[+-]\d{2}:\d{2}$/i.test(value) ? value : `${value}Z`;
    return new Date(normalized);
};

const formatTenantDateTime = (value: string, timeZone: string): string => {
    const date = parseUtcDate(value);
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat('es-PE', {
        timeZone,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
    }).format(date);
};

const getTipoColor = (tipo: string): string => {
    switch ((tipo ?? '').toLowerCase()) {
        case 'error': return tokens.colorPaletteRedBorderActive;
        case 'success': return tokens.colorPaletteGreenBorderActive;
        case 'warning': return tokens.colorPaletteMarigoldBorderActive;
        default: return tokens.colorPaletteBlueBorderActive;
    }
};

const getTipoIcon = (tipo: string) => {
    switch ((tipo ?? '').toLowerCase()) {
        case 'error': return <ErrorCircleFilled style={{ color: tokens.colorPaletteRedForeground1 }} />;
        case 'success': return <CheckmarkCircleFilled style={{ color: tokens.colorPaletteGreenForeground1 }} />;
        case 'warning': return <WarningFilled style={{ color: tokens.colorPaletteMarigoldForeground1 }} />;
        default: return <InfoFilled style={{ color: tokens.colorBrandForeground1 }} />;
    }
};

const LogsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();
    const { accounts } = useMsal();
    const account = accounts[0];

    const roles = ((account?.idTokenClaims as { roles?: string[] } | undefined)?.roles ?? []);
    const isIt = roles.includes('IT');

    const [logs, setLogs] = useState<TeamsLogOperativo[]>([]);
    const [summary, setSummary] = useState<LogOperationalSummaryDto | null>(null);
    const [companies, setCompanies] = useState<CompanyOption[]>([]);
    const [loading, setLoading] = useState(false);
    const [summaryLoading, setSummaryLoading] = useState(false);
    const [error, setError] = useState('');
    const [tenantTimeZone, setTenantTimeZone] = useState('America/Lima');
    const [viewMode, setViewMode] = useState<TabValue>('friendly');

    const [page, setPage] = useState(1);
    const [reloadTick, setReloadTick] = useState(0);

    const [tipoFiltro, setTipoFiltro] = useState('');
    const [severidadFiltro, setSeveridadFiltro] = useState('');
    const [entidadFiltro, setEntidadFiltro] = useState('');
    const [referenciaFiltro, setReferenciaFiltro] = useState('');
    const [jobIdFiltro, setJobIdFiltro] = useState('');
    const [searchInput, setSearchInput] = useState('');
    const [fechaDesde, setFechaDesde] = useState('');
    const [scopeFiltro, setScopeFiltro] = useState<'all' | 'current'>('current');
    const [companyFiltro, setCompanyFiltro] = useState('');

    const deferredSearch = useDeferredValue(searchInput.trim());

    useEffect(() => {
        setScopeFiltro(isIt ? 'all' : 'current');
        setCompanyFiltro('');
        setPage(1);
    }, [isIt]);

    useEffect(() => {
        if (!isIt) return;
        const loadCompanies = async () => {
            try {
                const response = await apiClient.get('/admin/company-configs');
                const data = Array.isArray(response.data) ? response.data : [];
                setCompanies(
                    data.map((item: any) => ({
                        companyKey: item.companyKey ?? '',
                        displayName: item.displayName ?? item.companyKey ?? ''
                    }))
                    .filter((item: CompanyOption) => item.companyKey)
                    .sort((a: CompanyOption, b: CompanyOption) => a.companyKey.localeCompare(b.companyKey))
                );
            } catch { setCompanies([]); }
        };
        void loadCompanies();
    }, [apiClient, isIt]);

    useEffect(() => {
        const loadTenantConfig = async () => {
            try {
                const response = await apiClient.get('/config');
                const config = (response.data ?? {}) as TenantConfigResponse;
                setTenantTimeZone(resolveBrowserTimeZone(config.timeZoneId));
            } catch { setTenantTimeZone('America/Lima'); }
        };
        void loadTenantConfig();
    }, [apiClient]);

    useEffect(() => {
        const fetchLogs = async () => {
            setLoading(true);
            setError('');
            try {
                const params: Record<string, string | number> = { page, pageSize: PAGE_SIZE };
                if (tipoFiltro) params.tipo = tipoFiltro;
                if (severidadFiltro) params.severidad = severidadFiltro;
                if (entidadFiltro) params.entidad = entidadFiltro;
                if (referenciaFiltro.trim()) params.referencia = referenciaFiltro.trim();
                if (jobIdFiltro.trim()) params.jobId = jobIdFiltro.trim();
                if (deferredSearch) params.search = deferredSearch;
                if (fechaDesde) params.fechaDesde = formatDateParam(fechaDesde);
                if (isIt) {
                    params.scope = scopeFiltro;
                    if (scopeFiltro === 'all' && companyFiltro) params.companyKey = companyFiltro;
                }
                const response = await apiClient.get('/reports/logs', { params });
                startTransition(() => setLogs(Array.isArray(response.data) ? response.data : []));
            } catch (err: any) {
                setError(err?.response?.data?.message || err?.message || 'No se pudo cargar el log.');
                startTransition(() => setLogs([]));
            } finally { setLoading(false); }
        };
        void fetchLogs();
    }, [apiClient, companyFiltro, deferredSearch, entidadFiltro, fechaDesde, isIt, jobIdFiltro, page, referenciaFiltro, reloadTick, scopeFiltro, severidadFiltro, tipoFiltro]);

    useEffect(() => {
        const fetchSummary = async () => {
            setSummaryLoading(true);
            try {
                const params: Record<string, string | number> = {};
                if (tipoFiltro) params.tipo = tipoFiltro;
                if (severidadFiltro) params.severidad = severidadFiltro;
                if (entidadFiltro) params.entidad = entidadFiltro;
                if (referenciaFiltro.trim()) params.referencia = referenciaFiltro.trim();
                if (jobIdFiltro.trim()) params.jobId = jobIdFiltro.trim();
                if (deferredSearch) params.search = deferredSearch;
                if (fechaDesde) params.fechaDesde = formatDateParam(fechaDesde);
                if (isIt) {
                    params.scope = scopeFiltro;
                    if (scopeFiltro === 'all' && companyFiltro) params.companyKey = companyFiltro;
                }
                const response = await apiClient.get('/reports/logs-summary', { params });
                setSummary(response.data);
            } catch (err) {
                console.error("Failed to fetch summary:", err);
            } finally {
                setSummaryLoading(false);
            }
        };
        void fetchSummary();
    }, [apiClient, companyFiltro, deferredSearch, entidadFiltro, fechaDesde, isIt, jobIdFiltro, reloadTick, scopeFiltro, severidadFiltro, tipoFiltro]);

    const entidadOptions = useMemo(() => {
        const values = new Set(logs.map(log => log.entidadAfectada).filter(Boolean));
        return Array.from(values).sort((a, b) => a.localeCompare(b));
    }, [logs]);

    const [clearingLogs, setClearingLogs] = useState(false);

    const handleRefresh = () => setReloadTick(current => current + 1);
    const handleClearFilters = () => {
        setTipoFiltro(''); setSeveridadFiltro(''); setEntidadFiltro(''); setReferenciaFiltro('');
        setJobIdFiltro(''); setSearchInput(''); setFechaDesde(''); setCompanyFiltro('');
        setPage(1); setReloadTick(current => current + 1);
    };

    const handleClearLogsFromDb = async () => {
        const confirm = await Swal.fire({
            title: '¿Borrar Historial de Logs?',
            text: 'Se eliminarán los registros de logs operativos del sistema de forma permanente.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#d33',
            cancelButtonColor: '#3085d6',
            confirmButtonText: 'Sí, borrar logs',
            cancelButtonText: 'Cancelar'
        });

        if (!confirm.isConfirmed) return;

        setClearingLogs(true);
        try {
            const params: Record<string, string> = {};
            if (tipoFiltro) params.tipo = tipoFiltro;
            if (severidadFiltro) params.severidad = severidadFiltro;
            if (entidadFiltro) params.entidad = entidadFiltro;
            if (isIt) {
                params.scope = scopeFiltro;
                if (scopeFiltro === 'all' && companyFiltro) params.companyKey = companyFiltro;
            }

            const res = await apiClient.delete('/reports/logs', { params });
            await Swal.fire('¡Borrado!', res.data?.message || 'Registros eliminados correctamente.', 'success');
            setReloadTick(c => c + 1);
        } catch (err: any) {
            await Swal.fire('Error', err?.response?.data?.message || 'No se pudieron borrar los logs.', 'error');
        } finally {
            setClearingLogs(false);
        }
    };

    const renderFriendlyLogs = () => (
        <div className={styles.friendlyGrid}>
            {logs.map(log => {
                const hasAction = log.mensaje.includes('Acción:');
                const parts = log.mensaje.split('Acción:');
                const mainMsg = parts[0]?.trim();
                const actionMsg = parts[1]?.trim();

                return (
                    <Card key={`${log.id}-${log.fecha}`} className={styles.logCard} style={{ borderLeftColor: getTipoColor(log.tipo) }}>
                        <CardHeader
                            image={getTipoIcon(log.tipo)}
                            header={
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                                    {log.companyKey && (
                                        <Text size={200} weight="bold" style={{ color: tokens.colorBrandForeground1, textTransform: 'uppercase', letterSpacing: '0.02em', marginBottom: '2px' }}>
                                            {log.companyKey}
                                        </Text>
                                    )}
                                    <Text weight="bold">{log.entidadAfectada} - {log.referencia}</Text>
                                    {log.usuario && (
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px' }}>
                                            <PersonRegular fontSize={12} style={{ color: tokens.colorNeutralForeground3 }} />
                                            <Text size={100} style={{ color: tokens.colorNeutralForeground3 }}>{log.usuario}</Text>
                                        </div>
                                    )}
                                </div>
                            }
                            description={<Caption1>{formatTenantDateTime(log.fecha, tenantTimeZone)}</Caption1>}
                            action={
                                <Badge appearance="outline" color={log.severidad === 'High' ? 'danger' : 'informative'}>
                                    {log.severidad || 'Low'}
                                </Badge>
                            }
                        />
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                            <Text size={300}>{mainMsg}</Text>
                            {hasAction && (
                                <div className={styles.cardAction}>
                                    <Text weight="semibold" style={{ color: tokens.colorBrandForeground1 }}>
                                        <WrenchRegular style={{ verticalAlign: 'middle', marginRight: '6px' }} />
                                        Acción Recomendada:
                                    </Text>
                                    <div style={{ marginTop: '4px' }}>{actionMsg}</div>
                                </div>
                            )}
                        </div>
                    </Card>
                );
            })}
        </div>
    );


    const renderOperationalSummary = () => {
        if (!summary) return null;

        const metrics = [
            { label: 'Alumnos Procesados', value: summary.studentsSuccess, icon: <PeopleCommunityRegular />, color: tokens.colorPaletteBlueForeground2, style: styles.cardStudents, decoration: <PeopleCommunityRegular /> },
            { label: 'Agendas Sincronizadas', value: summary.agendasSuccess, icon: <CalendarMonthRegular />, color: tokens.colorPalettePurpleForeground2, style: styles.cardAgendas, decoration: <CalendarMonthRegular /> },
            { label: 'Equipos Gestionados', value: summary.teamsSuccess, icon: <GroupRegular />, color: tokens.colorPaletteTealForeground2, style: styles.cardTeams, decoration: <GroupRegular /> },
            { label: 'Errores Críticos', value: summary.totalErrors, icon: <ErrorCircleRegular />, color: tokens.colorPaletteRedForeground1, style: styles.cardErrors, decoration: <ErrorCircleRegular /> },
            { label: 'Advertencias', value: summary.totalWarnings, icon: <WarningRegular />, color: tokens.colorPaletteMarigoldForeground1, style: styles.cardWarnings, decoration: <WarningRegular /> },
        ];

        return (
            <div className={styles.summaryContainer}>
                {metrics.map((m, i) => (
                    <div key={i} className={`${styles.summaryCard} ${m.style}`}>
                        <div className={styles.summaryIcon} style={{ color: m.color }}>{m.icon}</div>
                        <div>
                            {summaryLoading ? <Spinner size="tiny" /> : <div className={styles.summaryValue}>{m.value}</div>}
                            <div className={styles.summaryLabel}>{m.label}</div>
                        </div>
                        <div className={styles.summaryDecoration} style={{ color: m.color }}>{m.decoration}</div>
                    </div>
                ))}
            </div>
        );
    };

    const renderTechnicalLogs = () => (
        <div className={styles.technicalContainer}>
            <div className={styles.tableScroll}>
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell>Fecha / Job / Empresa</TableHeaderCell>
                            <TableHeaderCell>Tipo</TableHeaderCell>
                            <TableHeaderCell>Referencia</TableHeaderCell>
                            <TableHeaderCell>Usuario</TableHeaderCell>
                            <TableHeaderCell>Mensaje / Contexto</TableHeaderCell>
                            <TableHeaderCell>Acciones</TableHeaderCell>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {logs.map(log => (
                            <TableRow key={`${log.id}-${log.fecha}-tech`}>
                                <TableCell>
                                    <div style={{ fontWeight: 600 }}>{formatTenantDateTime(log.fecha, tenantTimeZone)}</div>
                                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center', marginTop: '2px' }}>
                                        {log.companyKey && (
                                            <Badge size="small" appearance="filled" style={{ backgroundColor: tokens.colorBrandBackground2, color: tokens.colorBrandForeground2, fontWeight: 'bold' }}>
                                                {log.companyKey}
                                            </Badge>
                                        )}
                                        <div style={{ fontFamily: 'monospace', fontSize: '11px', color: tokens.colorNeutralForeground3 }}>{log.jobId || 'N/A'}</div>
                                    </div>
                                </TableCell>
                                <TableCell>
                                    <Badge appearance="filled" color={
                                        log.tipo.toLowerCase() === 'error' ? 'danger' : 
                                        log.tipo.toLowerCase() === 'success' ? 'success' :
                                        log.tipo.toLowerCase() === 'warning' ? 'warning' : 'informative'
                                    }>
                                        {log.tipo}
                                    </Badge>
                                </TableCell>
                                <TableCell>
                                    <div>{log.entidadAfectada}</div>
                                    <code style={{ fontSize: '10px' }}>{log.referencia}</code>
                                </TableCell>
                                <TableCell>
                                    <Text size={200}>{log.usuario || 'Sistema'}</Text>
                                </TableCell>
                                <TableCell>
                                    <TextBlock data={log.mensaje} />
                                </TableCell>
                                <TableCell>
                                    <Dialog>
                                        <DialogTrigger disableButtonEnhancement>
                                            <Button icon={<OpenRegular />} size="small">Detalle</Button>
                                        </DialogTrigger>
                                        <DialogSurface>
                                            <DialogBody>
                                                <DialogTitle>Detalle Técnico del Log</DialogTitle>
                                                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '16px' }}>
                                                    <div>
                                                        <Subtitle2>Mensaje</Subtitle2>
                                                        <Text block>{log.mensaje}</Text>
                                                    </div>
                                                    <div>
                                                        <Subtitle2>Contexto Técnico (Stacktrace / RAW)</Subtitle2>
                                                        <div className={styles.mono}>{log.contextoTecnico || 'Sin contexto adicional.'}</div>
                                                    </div>
                                                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                                                        <div><Text block weight="semibold" size={200}>Referencia:</Text><Text block size={200}>{log.referencia}</Text></div>
                                                        <div><Text block weight="semibold" size={200}>JobId:</Text><Text block size={200}>{log.jobId || 'N/A'}</Text></div>
                                                        <div><Text block weight="semibold" size={200}>Ejecutado por:</Text><Text block size={200}>{log.usuario || 'Sistema'}</Text></div>
                                                    </div>
                                                </div>
                                                <DialogActions>
                                                    <DialogTrigger disableButtonEnhancement>
                                                        <Button appearance="secondary">Cerrar</Button>
                                                    </DialogTrigger>
                                                </DialogActions>
                                            </DialogBody>
                                        </DialogSurface>
                                    </Dialog>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </div>
        </div>
    );

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                    <div className={styles.headerIcon}>
                        <DocumentErrorRegular fontSize={32} color={tokens.colorBrandForeground1} />
                    </div>
                    <div>
                        <Title3>Trazabilidad Operacional</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2, fontWeight: 500 }}>
                            Monitoreo de equipos, agendas y alumnos en tiempo real
                        </div>
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '12px' }}>
                    <div className={styles.viewSelector}>
                        <TabList selectedValue={viewMode} onTabSelect={(_, d) => setViewMode(d.value)}>
                            <Tab value="friendly" icon={<PersonRegular />}>Administrador</Tab>
                            <Tab value="technical" icon={<CodeRegular />}>Técnico</Tab>
                        </TabList>
                    </div>
                    <Button icon={<ArrowClockwiseRegular />} appearance="primary" onClick={handleRefresh} disabled={loading}>
                        Refrescar
                    </Button>
                    <Button icon={<DeleteRegular />} appearance="outline" style={{ color: '#d13438', borderColor: '#d13438' }} onClick={handleClearLogsFromDb} disabled={clearingLogs || loading}>
                        Borrar Logs
                    </Button>
                </div>
            </div>

            {renderOperationalSummary()}

            <div className={styles.filterRegion}>
                <div className={styles.filterField} style={{ gridColumn: 'span 2' }}>
                    <label>Búsqueda Global</label>
                    <Input contentBefore={<SearchRegular />} placeholder="Buscar por mensaje, referencia o ID de job..." 
                           value={searchInput} onChange={(_, d) => { setSearchInput(d.value); setPage(1); }} />
                </div>
                <div className={styles.filterField}>
                    <label>Tipo</label>
                    <Select value={tipoFiltro} onChange={(_, d) => { setTipoFiltro(d.value); setPage(1); }}>
                        <option value="">Todos</option>
                        <option value="Info">Información</option>
                        <option value="Success">Éxito</option>
                        <option value="Warning">Advertencia</option>
                        <option value="Error">Error</option>
                    </Select>
                </div>
                <div className={styles.filterField}>
                    <label>Entidad</label>
                    <Select value={entidadFiltro} onChange={(_, d) => { setEntidadFiltro(d.value); setPage(1); }}>
                        <option value="">Todas</option>
                        {entidadOptions.map(o => <option key={o} value={o}>{o}</option>)}
                    </Select>
                </div>
                <div className={styles.filterField}>
                    <label>Desde</label>
                    <Input type="date" value={fechaDesde} onChange={(_, d) => { setFechaDesde(d.value); setPage(1); }} />
                </div>
                {isIt && (
                    <div className={styles.filterField}>
                        <label>Alcance</label>
                        <Select value={scopeFiltro} onChange={(_, d) => { setScopeFiltro(d.value as any); setPage(1); }}>
                            <option value="all">Multicompañía</option>
                            <option value="current">Empresa Actual</option>
                        </Select>
                    </div>
                )}
                {isIt && scopeFiltro === 'all' && (
                    <div className={styles.filterField}>
                        <label>Empresa</label>
                        <Select value={companyFiltro} onChange={(_, d) => { setCompanyFiltro(d.value); setPage(1); }}>
                            <option value="">(Todas)</option>
                            {companies.map(c => <option key={c.companyKey} value={c.companyKey}>{c.companyKey}</option>)}
                        </Select>
                    </div>
                )}
                <div className={styles.filterActions}>
                    <Button icon={<DismissRegular />} appearance="subtle" onClick={handleClearFilters}>Limpiar</Button>
                </div>
            </div>

            <div className={styles.contentArea}>
                {loading && logs.length === 0 ? (
                    <div style={{ textAlign: 'center', padding: '100px' }}><Spinner label="Procesando datos del log..." size="large" /></div>
                ) : logs.length === 0 ? (
                    <div className={styles.emptyState}>
                        <DraftsRegular fontSize={64} style={{ opacity: 0.2, marginBottom: '20px' }} />
                        <Title3 block>No se encontraron registros</Title3>
                        <Text>Intente ajustar los filtros de búsqueda.</Text>
                    </div>
                ) : (
                    viewMode === 'friendly' ? renderFriendlyLogs() : renderTechnicalLogs()
                )}
            </div>

            <div className={styles.footer}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <Badge appearance="outline" size="large">{logs.length} resultados</Badge>
                    {error && <Text style={{ color: tokens.colorPaletteRedForeground1 }}>{error}</Text>}
                </div>
                <div className={styles.pager}>
                    <Button disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Anterior</Button>
                    <Text weight="semibold">Página {page}</Text>
                    <Button disabled={logs.length < PAGE_SIZE || loading} onClick={() => setPage(p => p + 1)}>Siguiente</Button>
                </div>
            </div>
        </div>
    );
};

const TextBlock: React.FC<{ data: string }> = ({ data }) => (
    <Text size={200} block style={{ maxWidth: '500px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
        {data}
    </Text>
);

export default LogsPage;
