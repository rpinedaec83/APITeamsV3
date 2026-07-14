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
    Select,
    Spinner,
    Toolbar,
    ToolbarButton,
    Label,
    Button
} from '@fluentui/react-components';
import { 
    ArrowDownloadRegular, 
    ArrowClockwiseRegular, 
    TableRegular,
    AlertRegular,
    PersonRegular,
    CalendarAgendaRegular,
    CloudSyncRegular,
    HatGraduationRegular
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';

const useStyles = makeStyles({
    root: {
        padding: '32px',
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        maxWidth: '1400px',
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
    controlBar: {
        display: 'flex',
        gap: '16px',
        alignItems: 'end',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    tableContainer: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'auto',
        maxHeight: 'calc(100vh - 350px)',
        boxShadow: tokens.shadow2,
    }
});

type ReportType = 'schedules' | 'members' | 'consistency' | 'progress' | 'detalle-unidad';

const ReportsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    const [reportType, setReportType] = useState<ReportType>('progress');
    const [data, setData] = useState<any[]>([]);
    const [loading, setLoading] = useState(false);
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 50;

    const fetchReport = async () => {
        setLoading(true);
        try {
            let endpoint = '';
            switch (reportType) {
                case 'schedules': endpoint = '/reports/report-schedules'; break;
                case 'members': endpoint = '/reports/report-team-members'; break;
                case 'consistency': endpoint = '/reports/report-smart-vs-teams'; break;
                case 'progress': endpoint = '/reports/report-sync-progress'; break;
                case 'detalle-unidad': endpoint = '/reports/dashboard-summary'; break;
            }
            const response = await apiClient.get(endpoint);
            if (reportType === 'detalle-unidad') {
                setData(response.data.rows || []);
            } else {
                setData(response.data);
            }
        } catch (error) {
            console.error("Error fetching report", error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchReport();
        setCurrentPage(1);
    }, [reportType]);

    const exportToCsv = () => {
        if (data.length === 0) return;

        const headers = Object.keys(data[0]);
        const csvContent = [
            headers.join(','),
            ...data.map(row => headers.map(header => `"${row[header] ?? ''}"`).join(','))
        ].join('\n');

        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.setAttribute('href', url);
        link.setAttribute('download', `${reportType}_report_${new Date().toISOString().split('T')[0]}.csv`);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };

    const renderTableHeader = () => {
        if (data.length === 0) return null;
        const headers = Object.keys(data[0]);
        return (
            <TableRow>
                {headers.map(h => (
                    <TableHeaderCell key={h}>{h.charAt(0).toUpperCase() + h.slice(1).replace(/([A-Z])/g, ' $1')}</TableHeaderCell>
                ))}
            </TableRow>
        );
    };

    const renderTableBody = () => {
        if (data.length === 0) return (
            <TableRow>
                <TableCell colSpan={10} style={{ textAlign: 'center', padding: '40px' }}>
                    {loading ? <Spinner label="Cargando datos..." /> : "No se encontraron registros"}
                </TableCell>
            </TableRow>
        );

        const headers = Object.keys(data[0]);
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = startIndex + itemsPerPage;
        const currentData = data.slice(startIndex, endIndex);

        return currentData.map((row, i) => (
            <TableRow key={startIndex + i}>
                {headers.map(h => (
                    <TableCell key={h}>{String(row[h] ?? '')}</TableCell>
                ))}
            </TableRow>
        ));
    };

    const getIcon = () => {
        switch (reportType) {
            case 'schedules': return <CalendarAgendaRegular fontSize={32} color={tokens.colorBrandForeground1} />;
            case 'members': return <PersonRegular fontSize={32} color={tokens.colorBrandForeground1} />;
            case 'consistency': return <AlertRegular fontSize={32} color={tokens.colorBrandForeground1} />;
            case 'progress': return <CloudSyncRegular fontSize={32} color={tokens.colorBrandForeground1} />;
            case 'detalle-unidad': return <HatGraduationRegular fontSize={32} color={tokens.colorBrandForeground1} />;
            default: return <TableRegular fontSize={32} color={tokens.colorBrandForeground1} />;
        }
    };

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    {getIcon()}
                    <div>
                        <Title3>Reportes Avanzados</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>Inteligencia Operativa y Auditoría</div>
                    </div>
                </div>
                <Toolbar aria-label="Report actions">
                    <ToolbarButton icon={<ArrowClockwiseRegular />} onClick={fetchReport} disabled={loading}>Actualizar</ToolbarButton>
                    <ToolbarButton icon={<ArrowDownloadRegular />} onClick={exportToCsv} disabled={loading || data.length === 0}>Exportar CSV</ToolbarButton>
                </Toolbar>
            </div>

            <div className={styles.controlBar}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', minWidth: '300px' }}>
                    <Label>Seleccionar Reporte</Label>
                    <Select value={reportType} onChange={(_, data) => setReportType(data.value as ReportType)}>
                        <option value="progress">Avance de Sincronización</option>
                        <option value="schedules">Horarios y Sesiones</option>
                        <option value="members">Miembros y Roles por Team</option>
                        <option value="consistency">Consistencia Smart vs Teams</option>
                        <option value="detalle-unidad">Detalle por Unidad, Programa y Periodo</option>
                    </Select>
                </div>
                <div style={{ flexGrow: 1 }} />
                <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground4 }}>
                    {data.length} registros encontrados
                </div>
            </div>

            <div className={styles.tableContainer}>
                <Table size="extra-small" aria-label="Reporting table">
                    <TableHeader>
                        {renderTableHeader()}
                    </TableHeader>
                    <TableBody>
                        {renderTableBody()}
                    </TableBody>
                </Table>
            </div>

            {data.length > 0 && (
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px', backgroundColor: tokens.colorNeutralBackground1, borderRadius: tokens.borderRadiusLarge, boxShadow: tokens.shadow2, border: `1px solid ${tokens.colorNeutralStroke2}` }}>
                    <div style={{ fontSize: '14px', color: tokens.colorNeutralForeground1 }}>
                        Mostrando {((currentPage - 1) * itemsPerPage) + 1} a {Math.min(currentPage * itemsPerPage, data.length)} de {data.length} registros
                    </div>
                    <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                        <Button 
                            disabled={currentPage === 1} 
                            onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                        >
                            Anterior
                        </Button>
                        <span style={{ fontSize: '14px', margin: '0 8px', fontWeight: 'bold' }}>
                            Página {currentPage} de {Math.ceil(data.length / itemsPerPage)}
                        </span>
                        <Button 
                            disabled={currentPage === Math.ceil(data.length / itemsPerPage)} 
                            onClick={() => setCurrentPage(p => Math.min(Math.ceil(data.length / itemsPerPage), p + 1))}
                        >
                            Siguiente
                        </Button>
                    </div>
                </div>
            )}
        </div>
    );
};

export default ReportsPage;
