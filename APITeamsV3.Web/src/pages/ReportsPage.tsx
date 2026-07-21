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
    Select
} from '@fluentui/react-components';
import { 
    TableRegular,
    SearchRegular,
    ArrowDownloadRegular
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
        maxHeight: 'calc(100vh - 350px)',
    },
    tableCell: {
        padding: '8px 12px',
    }
});

const ReportsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    const [data, setData] = useState<any[]>([]);
    const [totalRecords, setTotalRecords] = useState(0);
    const [loading, setLoading] = useState(false);
    const [exporting, setExporting] = useState(false);
    
    // Pagination
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 50;
    
    // Summary Data for Options
    const [summaryRows, setSummaryRows] = useState<any[]>([]);

    // Filters
    const [filters, setFilters] = useState({
        smartSearch: '',
        unidad: '',
        programa: '',
        periodo: '',
        sede: '',
        docente: '',
        alumno: ''
    });

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

    useEffect(() => {
        fetchReport(1);
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

    const exportCurrentViewToExcel = () => {
        if (data.length === 0) return;

        const worksheet = XLSX.utils.json_to_sheet(data);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Reporte");
        XLSX.writeFile(workbook, `Reporte_${new Date().toISOString().split('T')[0]}.xlsx`);
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
                    
                    // Add API base url if needed, assuming the URL is relative to the API host
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
                // Don't stop polling on network error, but if we do we should setExporting(false)
            }
        }, 5000); // Poll every 5 seconds
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

    return (
        <div className={styles.root}>
            <div className={styles.panelCard}>
                <div className={styles.panelHeader}>
                    <div>
                        <Title3 style={{ margin: 0 }}>Reporte Detallado</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2, marginTop: '4px' }}>
                            Consulta de alumnos, docentes y equipos Teams
                        </div>
                    </div>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                        <Button onClick={exportAllToExcel} disabled={exporting || loading}>
                            {exporting ? 'Generando Reporte...' : 'Exportar Todo (Segundo Plano)'}
                        </Button>
                        <Button icon={<ArrowDownloadRegular />} onClick={exportCurrentViewToExcel} disabled={loading || data.length === 0}>Exportar Vista Actual</Button>
                        <TableRegular fontSize={24} color={tokens.colorBrandForeground1} />
                    </div>
                </div>

                <div className={styles.panelBody}>
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
                </div>
            </div>
        </div>
    );
};

export default ReportsPage;
