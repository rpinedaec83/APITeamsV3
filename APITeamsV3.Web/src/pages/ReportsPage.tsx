import React, { useState, useEffect } from 'react';
import {
    Table,
    TableHeader,
    TableRow,
    TableHeaderCell,
    TableBody,
    TableCell,
    Title3,
    makeStyles,
    shorthands,
    tokens,
    Spinner,
    Label,
    Button,
    Input,
    Select,
    TabList,
    Tab,
    Badge,
    Dialog,
    DialogSurface,
    DialogBody,
    DialogTitle,
    DialogContent,
    DialogActions
} from '@fluentui/react-components';
import { 
    TableRegular,
    SearchRegular,
    ArrowDownloadRegular,
    CheckmarkCircleRegular,
    DismissCircleRegular,
    BuildingRegular,
    EyeRegular,
    CalendarRegular,
    LinkRegular,
    WarningRegular
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';
import { getBaseApiUrl } from '../utils/config';
import * as XLSX from 'xlsx';
import Swal from 'sweetalert2';

const useStyles = makeStyles({
    root: {
        minHeight: '100%',
        padding: '28px',
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        background: 'radial-gradient(circle at 10% 20%, rgba(243, 248, 253, 1) 0%, rgba(228, 237, 248, 1) 90%)',
    },
    panelCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius('18px'),
        boxShadow: tokens.shadow8,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
    },
    panelHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '12px',
        ...shorthands.padding('18px', '20px'),
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: '#f7f9fc',
    },
    panelBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
        ...shorthands.padding('18px', '20px', '20px'),
    },
    filterBar: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '16px',
        alignItems: 'end',
    },
    filterItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
        minWidth: '150px'
    },
    tableContainer: {
        overflow: 'auto',
        maxHeight: 'calc(100vh - 380px)',
    },
    tableCell: {
        padding: '10px 14px',
    },
    statCardContainer: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
        gap: '16px',
        marginBottom: '8px'
    },
    statCard: {
        backgroundColor: 'white',
        borderRadius: '12px',
        padding: '16px 20px',
        boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
        border: '1px solid #e1dfdd',
        display: 'flex',
        alignItems: 'center',
        gap: '16px'
    },
    statIcon: {
        width: '42px',
        height: '42px',
        borderRadius: '10px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '22px'
    }
});

interface TeamSessionReportItem {
    idTeamsGroup: string;
    idSeccion: number;
    codigoSeccion: string;
    nombreTeam: string;
    periodo: string;
    totalSesiones: number;
    tieneSesiones: boolean;
    primeraSesion?: string | null;
    ultimaSesion?: string | null;
    correoFacilitador: string;
    motivoSinSesiones?: string;
    cantidadAlumnos?: number;
}

interface TeamSessionDetail {
    id: number;
    numeroReunion: number;
    codigo: string;
    fecha?: string | null;
    inicio?: string | null;
    fin?: string | null;
    correoFacilitador: string;
    joinUrl: string;
    estado: string;
}

const ReportsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    // Tab state
    const [activeTab, setActiveTab] = useState<'detailed' | 'sessions'>('detailed');

    // TAB 1: Detailed Report State
    const [data, setData] = useState<any[]>([]);
    const [totalRecords, setTotalRecords] = useState(0);
    const [loading, setLoading] = useState(false);
    const [exporting, setExporting] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 50;
    const [summaryRows, setSummaryRows] = useState<any[]>([]);

    const [filters, setFilters] = useState({
        smartSearch: '',
        unidad: '',
        programa: '',
        periodo: '',
        sede: '',
        docente: '',
        alumno: ''
    });

    // TAB 2: Team Sessions Report State
    const [sessionsData, setSessionsData] = useState<TeamSessionReportItem[]>([]);
    const [sessionsTotalRecords, setSessionsTotalRecords] = useState(0);
    const [sessionsLoading, setSessionsLoading] = useState(false);
    const [sessionsPage, setSessionsPage] = useState(1);
    const sessionsPerPage = 50;

    const [sessionsSummary, setSessionsSummary] = useState({
        totalEquipos: 0,
        equiposConSesiones: 0,
        equiposSinSesiones: 0,
        porcentajeCobertura: 0
    });

    const [sessionsFilters, setSessionsFilters] = useState({
        tieneSesiones: '', 
        periodo: '',
        search: '',
        docente: '',
        modoExcluirDocente: false,
        soloConAlumnos: '' // '' = Todos, 'true' = Con Alumnos (> 0), 'false' = Sin Alumnos (= 0)
    });

    // Detail Modal State
    const [detailModalOpen, setDetailModalOpen] = useState(false);
    const [selectedSectionInfo, setSelectedSectionInfo] = useState<{ idSeccion: number; codigoSeccion: string; nombreTeam: string } | null>(null);
    const [sessionDetails, setSessionDetails] = useState<TeamSessionDetail[]>([]);
    const [detailLoading, setDetailLoading] = useState(false);

    const handleFilterChange = (field: string, value: string) => {
        setFilters(prev => ({ ...prev, [field]: value }));
    };

    const fetchReport = async (page: number = currentPage) => {
        setLoading(true);
        try {
            const params = new URLSearchParams({
                PageNumber: page.toString(),
                PageSize: itemsPerPage.toString()
            });

            if (filters.smartSearch) params.append('SmartSearch', filters.smartSearch);
            if (filters.unidad) params.append('Unidad', filters.unidad);
            if (filters.programa) params.append('Programa', filters.programa);
            if (filters.periodo) params.append('Periodo', filters.periodo);
            if (filters.sede) params.append('Sede', filters.sede);
            if (filters.docente) params.append('Docente', filters.docente);
            if (filters.alumno) params.append('Alumno', filters.alumno);

            const response = await apiClient.get(`/reports/detailed-report?${params.toString()}`);
            setData(response.data.data || []);
            setTotalRecords(response.data.totalRecords || 0);
            setCurrentPage(page);
        } catch (error) {
            console.error("Error fetching report", error);
            Swal.fire('Error', 'No se pudo obtener el reporte.', 'error');
        } finally {
            setLoading(false);
        }
    };

    const fetchSessionsReport = async (page: number = sessionsPage) => {
        setSessionsLoading(true);
        try {
            const params = new URLSearchParams({
                pageNumber: page.toString(),
                pageSize: sessionsPerPage.toString()
            });

            if (sessionsFilters.tieneSesiones !== '') {
                params.append('tieneSesiones', sessionsFilters.tieneSesiones);
            }
            if (sessionsFilters.periodo) {
                params.append('periodo', sessionsFilters.periodo);
            }
            if (sessionsFilters.search) {
                params.append('search', sessionsFilters.search);
            }
            if (sessionsFilters.docente) {
                params.append('docente', sessionsFilters.docente);
            }
            if (sessionsFilters.modoExcluirDocente) {
                params.append('modoExcluirDocente', 'true');
            }
            if (sessionsFilters.soloConAlumnos !== '') {
                params.append('soloConAlumnos', sessionsFilters.soloConAlumnos);
            }

            const response = await apiClient.get(`/reports/team-sessions-report?${params.toString()}`);
            setSessionsData(response.data.data || []);
            setSessionsTotalRecords(response.data.totalRecords || 0);
            setSessionsSummary({
                totalEquipos: response.data.totalEquipos || 0,
                equiposConSesiones: response.data.equiposConSesiones || 0,
                equiposSinSesiones: response.data.equiposSinSesiones || 0,
                porcentajeCobertura: response.data.porcentajeCobertura || 0
            });
            setSessionsPage(page);
        } catch (error) {
            console.error("Error fetching sessions report", error);
            Swal.fire('Error', 'No se pudo obtener el reporte de sesiones por equipo.', 'error');
        } finally {
            setSessionsLoading(false);
        }
    };

    useEffect(() => {
        fetchReport(1);
        fetchSessionsReport(1);
        apiClient.get('/reports/dashboard-summary').then(res => {
            setSummaryRows(res.data.rows || []);
        }).catch(err => console.error("Error loading summary for options", err));
    }, []);

    const unidadOptions = Array.from(new Set(summaryRows.map(row => row.unidadNegocio as string))).sort();
    const programaOptions = Array.from(new Set(
        summaryRows
            .filter(row => !filters.unidad || row.unidadNegocio === filters.unidad)
            .map(row => row.programa as string)
    )).sort();
    const periodoOptions = Array.from(new Set(
        summaryRows
            .filter(row =>
                (!filters.unidad || row.unidadNegocio === filters.unidad) &&
                (!filters.programa || row.programa === filters.programa))
            .map(row => row.periodo as string)
    )).sort();
    const sedeOptions = Array.from(new Set(
        summaryRows
            .filter(row =>
                (!filters.unidad || row.unidadNegocio === filters.unidad) &&
                (!filters.programa || row.programa === filters.programa) &&
                (!filters.periodo || row.periodo === filters.periodo))
            .map(row => row.sede as string)
    )).sort();

    const openDetailModal = async (idSeccion: number, codigoSeccion: string, nombreTeam: string) => {
        setSelectedSectionInfo({ idSeccion, codigoSeccion, nombreTeam });
        setDetailModalOpen(true);
        setDetailLoading(true);
        try {
            const res = await apiClient.get(`/reports/team-sessions-report/${idSeccion}/details`);
            setSessionDetails(res.data || []);
        } catch (error) {
            console.error("Error loading session details", error);
            Swal.fire('Error', 'No se pudo cargar el detalle de sesiones.', 'error');
        } finally {
            setDetailLoading(false);
        }
    };

    const exportCurrentViewToExcel = () => {
        if (activeTab === 'detailed') {
            if (data.length === 0) return;
            const worksheet = XLSX.utils.json_to_sheet(data);
            const workbook = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(workbook, worksheet, "Reporte Detallado");
            XLSX.writeFile(workbook, `Reporte_Detallado_${new Date().toISOString().split('T')[0]}.xlsx`);
        } else {
            if (sessionsData.length === 0) return;
            const exportRows = sessionsData.map(item => ({
                'ID Sección': item.idSeccion,
                'Código Sección': item.codigoSeccion,
                'Nombre del Equipo': item.nombreTeam,
                'Periodo': item.periodo,
                'Alumnos Matriculados': item.cantidadAlumnos ?? 0,
                'Estado Sesiones': item.tieneSesiones ? 'Con Sesiones' : 'Sin Sesiones',
                'Motivo / Diagnóstico': item.tieneSesiones ? 'Agendado' : (item.motivoSinSesiones || 'Sin Sesiones'),
                'Total Sesiones': item.totalSesiones,
                'Primera Sesión': item.primeraSesion ? formatDate(item.primeraSesion) : '-',
                'Última Sesión': item.ultimaSesion ? formatDate(item.ultimaSesion) : '-',
                'Docente Facilitador': item.correoFacilitador || '-',
                'Team ID (M365)': item.idTeamsGroup
            }));
            const worksheet = XLSX.utils.json_to_sheet(exportRows);
            const workbook = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(workbook, worksheet, "Cobertura de Sesiones");
            XLSX.writeFile(workbook, `Reporte_Sesiones_Equipos_Vista_${new Date().toISOString().split('T')[0]}.xlsx`);
        }
    };

    const exportAllSessionsToExcel = async () => {
        setExporting(true);
        try {
            const params = new URLSearchParams({
                pageNumber: '1',
                pageSize: '100000'
            });

            if (sessionsFilters.tieneSesiones !== '') {
                params.append('tieneSesiones', sessionsFilters.tieneSesiones);
            }
            if (sessionsFilters.periodo) {
                params.append('periodo', sessionsFilters.periodo);
            }
            if (sessionsFilters.search) {
                params.append('search', sessionsFilters.search);
            }
            if (sessionsFilters.docente) {
                params.append('docente', sessionsFilters.docente);
            }
            if (sessionsFilters.modoExcluirDocente) {
                params.append('modoExcluirDocente', 'true');
            }
            if (sessionsFilters.soloConAlumnos !== '') {
                params.append('soloConAlumnos', sessionsFilters.soloConAlumnos);
            }

            const response = await apiClient.get(`/reports/team-sessions-report?${params.toString()}`);
            const allData: TeamSessionReportItem[] = response.data.data || [];

            if (allData.length === 0) {
                Swal.fire('Información', 'No hay registros para exportar.', 'info');
                return;
            }

            const exportRows = allData.map(item => ({
                'ID Sección': item.idSeccion,
                'Código Sección': item.codigoSeccion,
                'Nombre del Equipo': item.nombreTeam,
                'Periodo': item.periodo,
                'Alumnos Matriculados': item.cantidadAlumnos ?? 0,
                'Estado Sesiones': item.tieneSesiones ? 'Con Sesiones' : 'Sin Sesiones',
                'Motivo / Diagnóstico': item.tieneSesiones ? 'Agendado' : (item.motivoSinSesiones || 'Sin Sesiones'),
                'Total Sesiones': item.totalSesiones,
                'Primera Sesión': item.primeraSesion ? formatDate(item.primeraSesion) : '-',
                'Última Sesión': item.ultimaSesion ? formatDate(item.ultimaSesion) : '-',
                'Docente Facilitador': item.correoFacilitador || '-',
                'Team ID (M365)': item.idTeamsGroup
            }));

            const worksheet = XLSX.utils.json_to_sheet(exportRows);
            const workbook = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(workbook, worksheet, "Cobertura de Sesiones");
            const filename = `Reporte_Cobertura_Equipos_Todos_${new Date().toISOString().split('T')[0]}.xlsx`;
            XLSX.writeFile(workbook, filename);

            Swal.fire({
                title: '¡Exportación Exitosa!',
                text: `Se exportaron ${allData.length} registros a Excel (${filename}).`,
                icon: 'success',
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000
            });
        } catch (error) {
            console.error("Error al exportar todos los registros de sesiones", error);
            Swal.fire('Error', 'No se pudo exportar los registros a Excel.', 'error');
        } finally {
            setExporting(false);
        }
    };

    const exportAllToExcel = async () => {
        try {
            setExporting(true);
            const payload = {
                smartSearch: filters.smartSearch,
                unidad: filters.unidad,
                programa: filters.programa,
                periodo: filters.periodo,
                sede: filters.sede,
                docente: filters.docente,
                alumno: filters.alumno
            };

            const response = await apiClient.post('/reports/detailed-report/export', payload);
            const jobId = response.data.jobId;

            Swal.fire({
                title: 'Exportación iniciada',
                text: 'El reporte se está generando en segundo plano. Te notificaremos cuando esté listo.',
                icon: 'info',
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000
            });

            pollExportStatus(jobId);
        } catch (error) {
            console.error("Error starting export", error);
            Swal.fire('Error', 'No se pudo iniciar la exportación masiva.', 'error');
            setExporting(false);
        }
    };

    const pollExportStatus = async (jobId: string) => {
        const intervalId = setInterval(async () => {
            try {
                const res = await apiClient.get(`/reports/detailed-report/export/${jobId}`);
                if (res.data.status === 'Ready') {
                    clearInterval(intervalId);
                    setExporting(false);
                    const baseApiUrl = getBaseApiUrl();
                    const origin = new URL(baseApiUrl).origin;
                    const downloadUrl = `${origin}${res.data.url}`;

                    Swal.fire({
                        title: '¡Reporte Listo!',
                        html: `El reporte masivo ha terminado de generarse.<br><br><a href="${downloadUrl}" download class="swal2-confirm swal2-styled" style="text-decoration: none;">Descargar Excel</a>`,
                        icon: 'success',
                        showConfirmButton: false,
                        showCloseButton: true
                    });
                } else if (res.data.status === 'Error') {
                    clearInterval(intervalId);
                    setExporting(false);
                    Swal.fire('Error en Exportación', res.data.message || 'Ocurrió un problema generando el archivo.', 'error');
                }
            } catch (err) {
                console.error("Error polling status", err);
            }
        }, 5000);
    };

    const formatDate = (val?: string | null) => {
        if (!val) return '-';
        if (/^\d{4}-\d{2}-\d{2}/.test(val)) {
            const [datePart, timePart] = val.split('T');
            const [year, month, day] = datePart.split('-');
            return timePart ? `${day}/${month}/${year} ${timePart.substring(0, 5)}` : `${day}/${month}/${year}`;
        }
        return val;
    };

    const renderTableHeader = () => {
        if (data.length === 0) return null;
        const headers = Object.keys(data[0]);
        return (
            <TableRow>
                {headers.map(h => (
                    <TableHeaderCell key={h} className={styles.tableCell}>{h.charAt(0).toUpperCase() + h.slice(1).replace(/([A-Z])/g, ' $1')}</TableHeaderCell>
                ))}
            </TableRow>
        );
    };

    const renderTableBody = () => {
        if (loading) return (
            <TableRow>
                <TableCell colSpan={25} style={{ textAlign: 'center', padding: '40px' }}>
                    <Spinner label="Cargando datos..." />
                </TableCell>
            </TableRow>
        );

        if (data.length === 0) return (
            <TableRow>
                <TableCell colSpan={25} style={{ textAlign: 'center', padding: '40px' }}>
                    No se encontraron registros
                </TableCell>
            </TableRow>
        );

        const headers = Object.keys(data[0]);
        return data.map((row, i) => (
            <TableRow key={i}>
                {headers.map(h => {
                    let val = row[h];
                    if (typeof val === 'boolean') val = val ? 'Sí' : 'No';
                    if (typeof val === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(val)) {
                        const [datePart, timePart] = val.split('T');
                        const [year, month, day] = datePart.split('-');
                        if (h === 'fechaCreacionEquipoTeams' || h === 'FechaCreacionEquipoTeams') {
                            val = `${day}/${month}/${year} ${timePart.substring(0, 8)}`;
                        } else {
                            val = `${day}/${month}/${year}`;
                        }
                    }
                    return (
                        <TableCell key={h} className={styles.tableCell}>{String(val ?? '')}</TableCell>
                    );
                })}
            </TableRow>
        ));
    };

    const totalPages = Math.ceil(totalRecords / itemsPerPage);
    const sessionsTotalPages = Math.ceil(sessionsTotalRecords / sessionsPerPage);

    return (
        <div className={styles.root}>
            <div className={styles.panelCard}>
                <div className={styles.panelHeader}>
                    <div>
                        <Title3 style={{ margin: 0 }}>Reportes de Gestión de Teams</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2, marginTop: '4px' }}>
                            Consulta detallada de alumnos, docentes y cobertura de reuniones programadas por equipo
                        </div>
                    </div>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                        {activeTab === 'detailed' ? (
                            <Button onClick={exportAllToExcel} disabled={exporting || loading}>
                                {exporting ? 'Generando Reporte...' : 'Exportar Todo (Segundo Plano)'}
                            </Button>
                        ) : (
                            <Button 
                                appearance="primary" 
                                icon={<ArrowDownloadRegular />} 
                                onClick={exportAllSessionsToExcel} 
                                disabled={exporting || sessionsLoading || sessionsTotalRecords === 0}
                            >
                                {exporting ? 'Exportando Excel...' : 'Exportar Todos los Registros'}
                            </Button>
                        )}
                        <Button 
                            icon={<ArrowDownloadRegular />} 
                            onClick={exportCurrentViewToExcel} 
                            disabled={activeTab === 'detailed' ? (loading || data.length === 0) : (sessionsLoading || sessionsData.length === 0)}
                        >
                            Exportar Vista Actual
                        </Button>
                        <TableRegular fontSize={24} color={tokens.colorBrandForeground1} />
                    </div>
                </div>

                <div style={{ padding: '0 20px', borderBottom: `1px solid ${tokens.colorNeutralStroke2}`, backgroundColor: '#fcfdfe' }}>
                    <TabList selectedValue={activeTab} onTabSelect={(_, data) => setActiveTab(data.value as any)}>
                        <Tab value="detailed" icon={<TableRegular />}>Reporte Detallado de Registros</Tab>
                        <Tab value="sessions" icon={<CalendarRegular />}>Cobertura de Sesiones por Equipo</Tab>
                    </TabList>
                </div>

                <div className={styles.panelBody}>
                    {activeTab === 'detailed' && (
                        <>
                            <div className={styles.filterBar}>
                                <div className={styles.filterItem}>
                                    <Label>Búsqueda Inteligente</Label>
                                    <Input 
                                        placeholder="U, Prog, Per, Sede..." 
                                        value={filters.smartSearch} 
                                        onChange={(e) => handleFilterChange('smartSearch', e.target.value)}
                                    />
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Unidad</Label>
                                    <Select value={filters.unidad} onChange={(_: any, data: any) => handleFilterChange('unidad', data.value)}>
                                        <option value="">Todas</option>
                                        {unidadOptions.map(u => <option key={u} value={u}>{u}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Programa</Label>
                                    <Select value={filters.programa} onChange={(_: any, data: any) => handleFilterChange('programa', data.value)}>
                                        <option value="">Todos</option>
                                        {programaOptions.map(p => <option key={p} value={p}>{p}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Periodo</Label>
                                    <Select value={filters.periodo} onChange={(_: any, data: any) => handleFilterChange('periodo', data.value)}>
                                        <option value="">Todos</option>
                                        {periodoOptions.map(p => <option key={p} value={p}>{p}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Sede</Label>
                                    <Select value={filters.sede} onChange={(_: any, data: any) => handleFilterChange('sede', data.value)}>
                                        <option value="">Todas</option>
                                        {sedeOptions.map(s => <option key={s} value={s}>{s}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Docente</Label>
                                    <Input value={filters.docente} onChange={(e) => handleFilterChange('docente', e.target.value)} />
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Alumno</Label>
                                    <Input value={filters.alumno} onChange={(e) => handleFilterChange('alumno', e.target.value)} />
                                </div>
                                <Button 
                                    appearance="primary" 
                                    icon={<SearchRegular />} 
                                    onClick={() => fetchReport(1)}
                                    disabled={loading}
                                    style={{ marginBottom: '2px' }}
                                >
                                    Buscar
                                </Button>
                            </div>

                            <div className={styles.tableContainer}>
                                <Table aria-label="Reporting table" style={{ minWidth: 'max-content' }}>
                                    <TableHeader>
                                        {renderTableHeader()}
                                    </TableHeader>
                                    <TableBody>
                                        {renderTableBody()}
                                    </TableBody>
                                </Table>
                            </div>

                            {totalRecords > 0 && (
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px', backgroundColor: tokens.colorNeutralBackground1, borderRadius: tokens.borderRadiusLarge, boxShadow: tokens.shadow2, border: `1px solid ${tokens.colorNeutralStroke2}` }}>
                                    <div style={{ fontSize: '14px', color: tokens.colorNeutralForeground1 }}>
                                        Mostrando {((currentPage - 1) * itemsPerPage) + 1} a {Math.min(currentPage * itemsPerPage, totalRecords)} de {totalRecords} registros
                                    </div>
                                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                        <Button 
                                            disabled={currentPage === 1 || loading} 
                                            onClick={() => fetchReport(Math.max(1, currentPage - 1))}
                                        >
                                            Anterior
                                        </Button>
                                        <span style={{ fontSize: '14px', margin: '0 8px', fontWeight: 'bold' }}>
                                            Página {currentPage} de {totalPages}
                                        </span>
                                        <Button 
                                            disabled={currentPage === totalPages || loading} 
                                            onClick={() => fetchReport(Math.min(totalPages, currentPage + 1))}
                                        >
                                            Siguiente
                                        </Button>
                                    </div>
                                </div>
                            )}
                        </>
                    )}

                    {activeTab === 'sessions' && (
                        <>
                            <div className={styles.statCardContainer}>
                                <div className={styles.statCard}>
                                    <div className={styles.statIcon} style={{ background: '#eef4ff', color: '#005a9e' }}>
                                        <BuildingRegular />
                                    </div>
                                    <div>
                                        <div style={{ fontSize: '12px', color: '#605e5c', fontWeight: 600 }}>TOTAL EQUIPOS</div>
                                        <div style={{ fontSize: '24px', fontWeight: 800, color: '#1b1a19' }}>{sessionsSummary.totalEquipos}</div>
                                    </div>
                                </div>

                                <div className={styles.statCard}>
                                    <div className={styles.statIcon} style={{ background: '#e6f8ed', color: '#107c41' }}>
                                        <CheckmarkCircleRegular />
                                    </div>
                                    <div>
                                        <div style={{ fontSize: '12px', color: '#605e5c', fontWeight: 600 }}>CON SESIONES AGENDADAS</div>
                                        <div style={{ fontSize: '24px', fontWeight: 800, color: '#107c41' }}>{sessionsSummary.equiposConSesiones}</div>
                                    </div>
                                </div>

                                <div className={styles.statCard}>
                                    <div className={styles.statIcon} style={{ background: '#fde8e8', color: '#d13438' }}>
                                        <DismissCircleRegular />
                                    </div>
                                    <div>
                                        <div style={{ fontSize: '12px', color: '#605e5c', fontWeight: 600 }}>SIN SESIONES AGENDADAS</div>
                                        <div style={{ fontSize: '24px', fontWeight: 800, color: '#d13438' }}>{sessionsSummary.equiposSinSesiones}</div>
                                    </div>
                                </div>

                                <div className={styles.statCard}>
                                    <div className={styles.statIcon} style={{ background: '#fff4ce', color: '#856404' }}>
                                        <CalendarRegular />
                                    </div>
                                    <div>
                                        <div style={{ fontSize: '12px', color: '#605e5c', fontWeight: 600 }}>PORCENTAJE COBERTURA</div>
                                        <div style={{ fontSize: '24px', fontWeight: 800, color: '#005a9e' }}>{sessionsSummary.porcentajeCobertura}%</div>
                                    </div>
                                </div>
                            </div>

                            <div className={styles.filterBar}>
                                <div className={styles.filterItem}>
                                    <Label>Estado de Sesiones</Label>
                                    <Select 
                                        value={sessionsFilters.tieneSesiones} 
                                        onChange={(_: any, data: any) => setSessionsFilters(prev => ({ ...prev, tieneSesiones: data.value }))}
                                    >
                                        <option value="">Todos los Equipos</option>
                                        <option value="true">✅ Con Sesiones Agendadas</option>
                                        <option value="false">❌ Sin Sesiones Agendadas</option>
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Periodo</Label>
                                    <Select 
                                        value={sessionsFilters.periodo} 
                                        onChange={(_: any, data: any) => setSessionsFilters(prev => ({ ...prev, periodo: data.value }))}
                                    >
                                        <option value="">Todos los Periodos</option>
                                        {periodoOptions.map(p => <option key={p} value={p}>{p}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Alumnos</Label>
                                    <Select 
                                        value={sessionsFilters.soloConAlumnos} 
                                        onChange={(_: any, data: any) => setSessionsFilters(prev => ({ ...prev, soloConAlumnos: data.value }))}
                                    >
                                        <option value="">Todos los Equipos</option>
                                        <option value="true">👥 Con Alumnos (&gt; 0)</option>
                                        <option value="false">⚠️ Sin Alumnos (= 0)</option>
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>Modo Docente</Label>
                                    <Select value={sessionsFilters.modoExcluirDocente ? 'excluir' : 'incluir'} onChange={(_: any, data: any) => setSessionsFilters(prev => ({ ...prev, modoExcluirDocente: data.value === 'excluir' }))}>
                                        <option value="incluir">🔍 Buscar Docente</option>
                                        <option value="excluir">🚫 Excluir Docente</option>
                                    </Select>
                                </div>
                                <div className={styles.filterItem}>
                                    <Label>{sessionsFilters.modoExcluirDocente ? 'Docente a Excluir' : 'Docente'}</Label>
                                    <Input value={sessionsFilters.docente} onChange={(e) => setSessionsFilters(prev => ({ ...prev, docente: e.target.value }))} />
                                </div>
                                <div className={styles.filterItem} style={{ flexGrow: 1, minWidth: '220px' }}>
                                    <Label>Búsqueda (Código / Nombre / ID)</Label>
                                    <Input 
                                        placeholder="Ej. 1C004-PRH.26.00260..." 
                                        value={sessionsFilters.search} 
                                        onChange={(e) => setSessionsFilters(prev => ({ ...prev, search: e.target.value }))}
                                    />
                                </div>
                                <Button appearance="primary" icon={<SearchRegular />} onClick={() => fetchSessionsReport(1)} disabled={sessionsLoading}>Buscar</Button>
                            </div>

                            <div className={styles.tableContainer}>
                                <Table aria-label="Team sessions table">
                                    <TableHeader>
                                        <TableRow>
                                            <TableHeaderCell style={{ width: '80px' }}>ID Secc.</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '160px' }}>Código Sección</TableHeaderCell>
                                            <TableHeaderCell>Nombre de Equipo Teams</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '100px' }}>Periodo</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '100px', textAlign: 'center' }}>Alumnos</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '140px' }}>Estado Sesiones</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '220px' }}>Rango de Fechas</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '180px' }}>Facilitador / Docente</TableHeaderCell>
                                            <TableHeaderCell style={{ width: '120px', textAlign: 'center' }}>Acciones</TableHeaderCell>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {sessionsLoading ? (
                                            <TableRow>
                                                <TableCell colSpan={9} style={{ textAlign: 'center', padding: '40px' }}>
                                                    <Spinner label="Cargando cobertura de sesiones..." />
                                                </TableCell>
                                            </TableRow>
                                        ) : sessionsData.length === 0 ? (
                                            <TableRow>
                                                <TableCell colSpan={9} style={{ textAlign: 'center', padding: '40px' }}>
                                                    No se encontraron equipos con los criterios seleccionados.
                                                </TableCell>
                                            </TableRow>
                                        ) : (
                                            sessionsData.map((item) => (
                                                <TableRow key={item.idTeamsGroup || item.idSeccion}>
                                                    <TableCell className={styles.tableCell}><b>{item.idSeccion}</b></TableCell>
                                                    <TableCell className={styles.tableCell}><code>{item.codigoSeccion}</code></TableCell>
                                                    <TableCell className={styles.tableCell}><b>{item.nombreTeam}</b></TableCell>
                                                    <TableCell className={styles.tableCell}>{item.periodo || '-'}</TableCell>
                                                    <TableCell className={styles.tableCell} style={{ textAlign: 'center' }}>
                                                        <Badge appearance="tint" color="brand" style={{ fontWeight: 600 }}>
                                                            👥 {item.cantidadAlumnos ?? 0}
                                                        </Badge>
                                                    </TableCell>
                                                    <TableCell className={styles.tableCell}>
                                                        {item.tieneSesiones ? (
                                                            <Badge appearance="filled" color="success">
                                                                ✅ {item.totalSesiones} {item.totalSesiones === 1 ? 'Sesión' : 'Sesiones'}
                                                            </Badge>
                                                        ) : (
                                                            <div>
                                                                <Badge appearance="filled" color="danger">
                                                                    ❌ Sin Sesiones
                                                                </Badge>
                                                                {item.motivoSinSesiones && (
                                                                    <div style={{ fontSize: '11px', color: '#d13438', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '4px', marginTop: '4px' }}>
                                                                        <WarningRegular style={{ fontSize: '12px' }} />
                                                                        <span>{item.motivoSinSesiones}</span>
                                                                    </div>
                                                                )}
                                                            </div>
                                                        )}
                                                    </TableCell>
                                                    <TableCell className={styles.tableCell} style={{ fontSize: '12px' }}>
                                                        {item.tieneSesiones ? (
                                                            <div>
                                                                <span>Del: {formatDate(item.primeraSesion)}</span><br/>
                                                                <span>Al: {formatDate(item.ultimaSesion)}</span>
                                                            </div>
                                                        ) : (
                                                            <span style={{ color: '#a19f9d' }}>-</span>
                                                        )}
                                                    </TableCell>
                                                    <TableCell className={styles.tableCell} style={{ fontSize: '12px' }}>
                                                        {item.correoFacilitador ? <code>{item.correoFacilitador}</code> : '-'}
                                                    </TableCell>
                                                    <TableCell className={styles.tableCell} style={{ textAlign: 'center' }}>
                                                        <Button
                                                            size="small"
                                                            icon={<EyeRegular />}
                                                            disabled={!item.tieneSesiones}
                                                            onClick={() => openDetailModal(item.idSeccion, item.codigoSeccion, item.nombreTeam)}
                                                        >
                                                            Ver Detalle
                                                        </Button>
                                                    </TableCell>
                                                </TableRow>
                                            ))
                                        )}
                                    </TableBody>
                                </Table>
                            </div>

                            {/* Pagination */}
                            {sessionsTotalRecords > 0 && (
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px', backgroundColor: tokens.colorNeutralBackground1, borderRadius: tokens.borderRadiusLarge, boxShadow: tokens.shadow2, border: `1px solid ${tokens.colorNeutralStroke2}` }}>
                                    <div style={{ fontSize: '14px', color: tokens.colorNeutralForeground1 }}>
                                        Mostrando {((sessionsPage - 1) * sessionsPerPage) + 1} a {Math.min(sessionsPage * sessionsPerPage, sessionsTotalRecords)} de {sessionsTotalRecords} equipos
                                    </div>
                                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                        <Button 
                                            disabled={sessionsPage === 1 || sessionsLoading} 
                                            onClick={() => fetchSessionsReport(Math.max(1, sessionsPage - 1))}
                                        >
                                            Anterior
                                        </Button>
                                        <span style={{ fontSize: '14px', fontWeight: 600 }}>Página {sessionsPage} de {sessionsTotalPages}</span>
                                        <Button 
                                            disabled={sessionsPage >= sessionsTotalPages || sessionsLoading} 
                                            onClick={() => fetchSessionsReport(sessionsPage + 1)}
                                        >
                                            Siguiente
                                        </Button>
                                    </div>
                                </div>
                            )}
                        </>
                    )}
                </div>
            </div>

            {/* Modal Dialog: Detalle de Sesiones Programadas */}
            <Dialog open={detailModalOpen} onOpenChange={(_, data) => setDetailModalOpen(data.open)}>
                <DialogSurface style={{ maxWidth: '850px', width: '90%' }}>
                    <DialogBody>
                        <DialogTitle>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                <CalendarRegular style={{ fontSize: '24px', color: '#005a9e' }} />
                                <span>Detalle de Sesiones de Clase - {selectedSectionInfo?.codigoSeccion}</span>
                            </div>
                        </DialogTitle>
                        <DialogContent style={{ marginTop: '16px' }}>
                            <div style={{ marginBottom: '12px', fontSize: '14px', color: '#605e5c' }}>
                                <b>Equipo:</b> {selectedSectionInfo?.nombreTeam} (ID Sección: <b>{selectedSectionInfo?.idSeccion}</b>)
                            </div>

                            {detailLoading ? (
                                <div style={{ padding: '40px', textAlign: 'center' }}>
                                    <Spinner label="Cargando agendas programadas..." />
                                </div>
                            ) : sessionDetails.length === 0 ? (
                                <div style={{ padding: '30px', textAlign: 'center', color: '#605e5c' }}>
                                    No se encontraron detalles de reuniones para este equipo.
                                </div>
                            ) : (
                                <div style={{ overflowX: 'auto', maxHeight: '420px' }}>
                                    <Table style={{ tableLayout: 'fixed', width: '100%' }}>
                                        <TableHeader>
                                            <TableRow>
                                                <TableHeaderCell style={{ width: '70px' }}># Sesión</TableHeaderCell>
                                                <TableHeaderCell style={{ width: '120px' }}>Fecha</TableHeaderCell>
                                                <TableHeaderCell style={{ width: '160px' }}>Horario (Inicio - Fin)</TableHeaderCell>
                                                <TableHeaderCell>Docente Facilitador</TableHeaderCell>
                                                <TableHeaderCell style={{ width: '120px', textAlign: 'center' }}>Enlace Teams</TableHeaderCell>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {sessionDetails.map(s => (
                                                <TableRow key={s.id || s.numeroReunion}>
                                                    <TableCell><b>N° {s.numeroReunion}</b></TableCell>
                                                    <TableCell>{formatDate(s.fecha)}</TableCell>
                                                    <TableCell style={{ fontSize: '12px' }}>
                                                        {s.inicio ? formatDate(s.inicio).split(' ')[1] || '-' : '-'} - {s.fin ? formatDate(s.fin).split(' ')[1] || '-' : '-'}
                                                    </TableCell>
                                                    <TableCell style={{ fontSize: '12px' }}>
                                                        <code>{s.correoFacilitador || '-'}</code>
                                                    </TableCell>
                                                    <TableCell style={{ textAlign: 'center' }}>
                                                        {s.joinUrl ? (
                                                            <Button
                                                                size="small"
                                                                icon={<LinkRegular />}
                                                                as="a"
                                                                href={s.joinUrl}
                                                                target="_blank"
                                                                rel="noopener noreferrer"
                                                                appearance="subtle"
                                                            >
                                                                Unirse
                                                            </Button>
                                                        ) : (
                                                            <span style={{ fontSize: '12px', color: '#a19f9d' }}>Sin URL</span>
                                                        )}
                                                    </TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                </div>
                            )}
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={() => setDetailModalOpen(false)}>
                                Cerrar
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default ReportsPage;
