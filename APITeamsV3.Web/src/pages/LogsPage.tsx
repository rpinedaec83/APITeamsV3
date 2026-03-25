import React, { useState, useEffect } from 'react';
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
    Select
} from '@fluentui/react-components';
import { SearchRegular, ArrowClockwiseRegular, DocumentErrorRegular } from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';

interface TeamsLogOperativo {
    id: number;
    tipo: string;
    entidadAfectada: string;
    referencia: string;
    mensaje: string;
    severidad: string;
    jobId: string;
    fecha: string;
}

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
    filterRegion: {
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
        overflow: 'hidden',
        boxShadow: tokens.shadow2,
    }
});

const LogsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    const [logs, setLogs] = useState<TeamsLogOperativo[]>([]);
    const [loading, setLoading] = useState(false);
    const [tipoFiltro, setTipoFiltro] = useState<string>('');
    const [page, setPage] = useState(1);

    const fetchLogs = async () => {
        setLoading(true);
        try {
            const endpoint = `/reports/logs?page=${page}&pageSize=50${tipoFiltro ? `&tipo=${tipoFiltro}` : ''}`;
            const response = await apiClient.get(endpoint);
            setLogs(response.data);
        } catch (error) {
            console.error("Error fetching logs", error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchLogs();
    }, [page, tipoFiltro]);

    const handleRefresh = () => {
        if (page === 1) {
            fetchLogs();
        } else {
            setPage(1);
        }
    };

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <DocumentErrorRegular fontSize={32} color={tokens.colorBrandForeground1} />
                    <div>
                        <Title3>Log Operacional</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>Trazabilidad de eventos e inconsistencias</div>
                    </div>
                </div>
                <div>
                    <Button icon={<ArrowClockwiseRegular />} appearance="primary" onClick={handleRefresh} disabled={loading}>Refrescar</Button>
                </div>
            </div>

            <div className={styles.filterRegion}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', minWidth: '200px' }}>
                    <label>Tipo de Evento</label>
                    <Select value={tipoFiltro} onChange={(_, data) => setTipoFiltro(data.value)}>
                        <option value="">(Todos)</option>
                        <option value="Info">Informativo</option>
                        <option value="Success">Éxito</option>
                        <option value="Error">Error</option>
                    </Select>
                </div>
                <Button icon={<SearchRegular />} onClick={handleRefresh} disabled={loading}>Buscar</Button>
            </div>

            <div className={styles.tableContainer}>
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell>Fecha</TableHeaderCell>
                            <TableHeaderCell>Tipo</TableHeaderCell>
                            <TableHeaderCell>Entidad</TableHeaderCell>
                            <TableHeaderCell>Referencia</TableHeaderCell>
                            <TableHeaderCell>Mensaje</TableHeaderCell>
                            <TableHeaderCell>Job ID</TableHeaderCell>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {logs.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={6} style={{ textAlign: 'center', padding: '20px' }}>No hay registros para mostrar</TableCell>
                            </TableRow>
                        ) : (
                            logs.map(log => (
                                <TableRow key={log.id}>
                                    <TableCell>{new Date(log.fecha).toLocaleString()}</TableCell>
                                    <TableCell>
                                        <Badge 
                                            appearance="filled" 
                                            color={log.tipo === 'Error' ? 'danger' : log.tipo === 'Success' ? 'success' : 'informative'}
                                        >
                                            {log.tipo}
                                        </Badge>
                                    </TableCell>
                                    <TableCell>{log.entidadAfectada}</TableCell>
                                    <TableCell style={{ fontFamily: 'monospace' }}>{log.referencia}</TableCell>
                                    <TableCell>{log.mensaje}</TableCell>
                                    <TableCell style={{ fontFamily: 'monospace', fontSize: '11px' }}>{log.jobId}</TableCell>
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
                
                <div style={{ padding: '16px', display: 'flex', justifyContent: 'center', gap: '10px' }}>
                    <Button disabled={page === 1} onClick={() => setPage(page - 1)}>Anterior</Button>
                    <span style={{ display: 'flex', alignItems: 'center' }}>Página {page}</span>
                    <Button disabled={logs.length < 50} onClick={() => setPage(page + 1)}>Siguiente</Button>
                </div>
            </div>
        </div>
    );
};

export default LogsPage;
