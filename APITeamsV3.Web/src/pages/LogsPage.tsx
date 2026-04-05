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
    Spinner
} from '@fluentui/react-components';
import { ArrowClockwiseRegular, DismissRegular, DocumentErrorRegular, FilterRegular, SearchRegular } from '@fluentui/react-icons';
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
    fecha: string;
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
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px', '24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow4,
    },
    filterRegion: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: '14px',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    filterField: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    filterActions: {
        display: 'flex',
        gap: '10px',
        alignItems: 'end',
        justifyContent: 'flex-end',
        gridColumn: '1 / -1',
    },
    tableContainer: {
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
        padding: '16px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    pager: {
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
    },
    meta: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
    },
    errorText: {
        color: tokens.colorPaletteRedForeground1,
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
    }
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
    if (Number.isNaN(date.getTime())) {
        return value;
    }

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

const getTipoBadgeColor = (tipo: string): 'danger' | 'success' | 'warning' | 'informative' => {
    switch ((tipo ?? '').toLowerCase()) {
        case 'error':
            return 'danger';
        case 'success':
            return 'success';
        case 'warning':
            return 'warning';
        default:
            return 'informative';
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
    const [companies, setCompanies] = useState<CompanyOption[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [tenantTimeZone, setTenantTimeZone] = useState('America/Lima');

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
                    data
                        .map((item: any) => ({
                            companyKey: item.companyKey ?? '',
                            displayName: item.displayName ?? item.companyKey ?? ''
                        }))
                        .filter((item: CompanyOption) => item.companyKey)
                        .sort((a: CompanyOption, b: CompanyOption) => a.companyKey.localeCompare(b.companyKey))
                );
            } catch {
                setCompanies([]);
            }
        };

        void loadCompanies();
    }, [apiClient, isIt]);

    useEffect(() => {
        const loadTenantConfig = async () => {
            try {
                const response = await apiClient.get('/config');
                const config = (response.data ?? {}) as TenantConfigResponse;
                setTenantTimeZone(resolveBrowserTimeZone(config.timeZoneId));
            } catch {
                setTenantTimeZone('America/Lima');
            }
        };

        void loadTenantConfig();
    }, [apiClient]);

    useEffect(() => {
        const fetchLogs = async () => {
            setLoading(true);
            setError('');

            try {
                const params: Record<string, string | number> = {
                    page,
                    pageSize: PAGE_SIZE
                };

                if (tipoFiltro) params.tipo = tipoFiltro;
                if (severidadFiltro) params.severidad = severidadFiltro;
                if (entidadFiltro) params.entidad = entidadFiltro;
                if (referenciaFiltro.trim()) params.referencia = referenciaFiltro.trim();
                if (jobIdFiltro.trim()) params.jobId = jobIdFiltro.trim();
                if (deferredSearch) params.search = deferredSearch;
                if (fechaDesde) params.fechaDesde = formatDateParam(fechaDesde);

                if (isIt) {
                    params.scope = scopeFiltro;
                    if (scopeFiltro === 'all' && companyFiltro) {
                        params.companyKey = companyFiltro;
                    }
                }

                const response = await apiClient.get('/reports/logs', { params });
                const data = Array.isArray(response.data) ? response.data : [];
                startTransition(() => setLogs(data));
            } catch (err: any) {
                const message = err?.response?.data?.message || err?.message || 'No se pudo cargar el log operacional.';
                setError(message);
                startTransition(() => setLogs([]));
            } finally {
                setLoading(false);
            }
        };

        void fetchLogs();
    }, [
        apiClient,
        companyFiltro,
        deferredSearch,
        entidadFiltro,
        fechaDesde,
        isIt,
        jobIdFiltro,
        page,
        referenciaFiltro,
        reloadTick,
        scopeFiltro,
        severidadFiltro,
        tipoFiltro
    ]);

    const entidadOptions = useMemo(() => {
        const values = new Set(logs.map(log => log.entidadAfectada).filter(Boolean));
        if (entidadFiltro) values.add(entidadFiltro);
        return Array.from(values).sort((a, b) => a.localeCompare(b));
    }, [entidadFiltro, logs]);

    const severidadOptions = useMemo(() => {
        const values = new Set(logs.map(log => log.severidad).filter(Boolean));
        if (severidadFiltro) values.add(severidadFiltro);
        return Array.from(values).sort((a, b) => a.localeCompare(b));
    }, [logs, severidadFiltro]);

    const showCompanyColumn = isIt && scopeFiltro === 'all';
    const columnCount = showCompanyColumn ? 7 : 6;

    const handleRefresh = () => setReloadTick(current => current + 1);

    const handleClearFilters = () => {
        setTipoFiltro('');
        setSeveridadFiltro('');
        setEntidadFiltro('');
        setReferenciaFiltro('');
        setJobIdFiltro('');
        setSearchInput('');
        setFechaDesde('');
        setCompanyFiltro('');
        setPage(1);
        setReloadTick(current => current + 1);
    };

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <DocumentErrorRegular fontSize={32} color={tokens.colorBrandForeground1} />
                    <div>
                        <Title3>Log Operacional</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>
                            Filtros inteligentes y trazabilidad por empresa según rol
                        </div>
                    </div>
                </div>
                <Button icon={<ArrowClockwiseRegular />} appearance="primary" onClick={handleRefresh} disabled={loading}>
                    Refrescar
                </Button>
            </div>

            <div className={styles.filterRegion}>
                <div className={styles.filterField}>
                    <label>Búsqueda inteligente</label>
                    <Input
                        contentBefore={<SearchRegular />}
                        placeholder="Mensaje, referencia, Job ID, entidad..."
                        value={searchInput}
                        onChange={(_, data) => {
                            setSearchInput(data.value);
                            setPage(1);
                        }}
                    />
                </div>

                <div className={styles.filterField}>
                    <label>Tipo de Evento</label>
                    <Select value={tipoFiltro} onChange={(_, data) => { setTipoFiltro(data.value); setPage(1); }}>
                        <option value="">(Todos)</option>
                        <option value="Info">Info</option>
                        <option value="Success">Success</option>
                        <option value="Warning">Warning</option>
                        <option value="Error">Error</option>
                    </Select>
                </div>

                <div className={styles.filterField}>
                    <label>Severidad</label>
                    <Select value={severidadFiltro} onChange={(_, data) => { setSeveridadFiltro(data.value); setPage(1); }}>
                        <option value="">(Todas)</option>
                        {severidadOptions.map(severidad => (
                            <option key={severidad} value={severidad}>{severidad}</option>
                        ))}
                    </Select>
                </div>

                <div className={styles.filterField}>
                    <label>Entidad</label>
                    <Select value={entidadFiltro} onChange={(_, data) => { setEntidadFiltro(data.value); setPage(1); }}>
                        <option value="">(Todas)</option>
                        {entidadOptions.map(entidad => (
                            <option key={entidad} value={entidad}>{entidad}</option>
                        ))}
                    </Select>
                </div>

                <div className={styles.filterField}>
                    <label>Referencia</label>
                    <Input
                        placeholder="Ej. 410087 o GUID"
                        value={referenciaFiltro}
                        onChange={(_, data) => {
                            setReferenciaFiltro(data.value);
                            setPage(1);
                        }}
                    />
                </div>

                <div className={styles.filterField}>
                    <label>Job ID</label>
                    <Input
                        placeholder="Filtrar por Job ID"
                        value={jobIdFiltro}
                        onChange={(_, data) => {
                            setJobIdFiltro(data.value);
                            setPage(1);
                        }}
                    />
                </div>

                <div className={styles.filterField}>
                    <label>Desde Fecha</label>
                    <Input
                        type="date"
                        value={fechaDesde}
                        onChange={(_, data) => {
                            setFechaDesde(data.value);
                            setPage(1);
                        }}
                    />
                </div>

                {isIt && (
                    <div className={styles.filterField}>
                        <label>Alcance</label>
                        <Select value={scopeFiltro} onChange={(_, data) => { setScopeFiltro(data.value === 'current' ? 'current' : 'all'); setPage(1); }}>
                            <option value="all">Todas las empresas</option>
                            <option value="current">Solo empresa actual</option>
                        </Select>
                    </div>
                )}

                {isIt && scopeFiltro === 'all' && (
                    <div className={styles.filterField}>
                        <label>Empresa</label>
                        <Select value={companyFiltro} onChange={(_, data) => { setCompanyFiltro(data.value); setPage(1); }}>
                            <option value="">(Todas)</option>
                            {companies.map(company => (
                                <option key={company.companyKey} value={company.companyKey}>
                                    {company.companyKey} - {company.displayName}
                                </option>
                            ))}
                        </Select>
                    </div>
                )}

                <div className={styles.filterActions}>
                    <Button icon={<FilterRegular />} onClick={handleRefresh} disabled={loading}>
                        Aplicar
                    </Button>
                    <Button icon={<DismissRegular />} appearance="subtle" onClick={handleClearFilters} disabled={loading}>
                        Limpiar Filtros
                    </Button>
                </div>
            </div>

            <div className={styles.tableContainer}>
                <div className={styles.tableScroll}>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>Fecha</TableHeaderCell>
                                <TableHeaderCell>Tipo</TableHeaderCell>
                                {showCompanyColumn && <TableHeaderCell>Empresa</TableHeaderCell>}
                                <TableHeaderCell>Entidad</TableHeaderCell>
                                <TableHeaderCell>Referencia</TableHeaderCell>
                                <TableHeaderCell>Mensaje</TableHeaderCell>
                                <TableHeaderCell>Job ID</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {loading && logs.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={columnCount} style={{ textAlign: 'center', padding: '20px' }}>
                                        <Spinner label="Cargando logs..." />
                                    </TableCell>
                                </TableRow>
                            ) : logs.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={columnCount} style={{ textAlign: 'center', padding: '20px' }}>
                                        No hay registros para mostrar con los filtros actuales.
                                    </TableCell>
                                </TableRow>
                            ) : (
                                logs.map(log => (
                                    <TableRow key={`${log.companyKey ?? 'tenant'}-${log.id}-${log.fecha}`}>
                                        <TableCell>{formatTenantDateTime(log.fecha, tenantTimeZone)}</TableCell>
                                        <TableCell>
                                            <Badge appearance="filled" color={getTipoBadgeColor(log.tipo)}>
                                                {log.tipo}
                                            </Badge>
                                        </TableCell>
                                        {showCompanyColumn && <TableCell style={{ fontFamily: 'monospace' }}>{log.companyKey}</TableCell>}
                                        <TableCell>{log.entidadAfectada}</TableCell>
                                        <TableCell style={{ fontFamily: 'monospace' }}>{log.referencia}</TableCell>
                                        <TableCell>{log.mensaje}</TableCell>
                                        <TableCell style={{ fontFamily: 'monospace', fontSize: '11px' }}>{log.jobId}</TableCell>
                                    </TableRow>
                                ))
                            )}
                        </TableBody>
                    </Table>
                </div>

                <div className={styles.footer}>
                    <div className={styles.meta}>
                        <span>{logs.length} registros en esta página</span>
                        {error && <span className={styles.errorText}>{error}</span>}
                    </div>
                    <div className={styles.pager}>
                        <Button disabled={page === 1 || loading} onClick={() => setPage(current => current - 1)}>
                            Anterior
                        </Button>
                        <span>Página {page}</span>
                        <Button disabled={logs.length < PAGE_SIZE || loading} onClick={() => setPage(current => current + 1)}>
                            Siguiente
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LogsPage;
