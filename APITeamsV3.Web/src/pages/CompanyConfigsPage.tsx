
import React, { useEffect, useState } from 'react';
import type { CompanyConfig, CreateCompanyConfigRequest, UpdateCompanyConfigRequest } from '../types/CompanyConfig';
import type { Sede } from '../types/Sede';
import { useApiClient } from '../hooks/useApiClient';
import {
    Button,
    Table,
    TableHeader,
    TableRow,
    TableHeaderCell,
    TableBody,
    TableCell,
    Dialog,
    DialogSurface,
    DialogBody,
    DialogTitle,
    DialogContent,
    DialogActions,
    Input,
    Label,
    Switch,
    Badge,
    Spinner,
    Tooltip
} from '@fluentui/react-components';
import { DeleteRegular, EditRegular, AddRegular, BuildingRegular, ArrowSyncRegular } from '@fluentui/react-icons';
import { showError, showConfirm } from '../utils/alerts';

type ConnectionValidationResult = {
    id: number;
    companyKey: string;
    displayName: string;
    isValid: boolean;
    errorMessage?: string | null;
};

const CompanyConfigsPage: React.FC = () => {
    const api = useApiClient();
    const [configs, setConfigs] = useState<CompanyConfig[]>([]);
    const [isOpen, setIsOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [currentConfig, setCurrentConfig] = useState<Partial<CompanyConfig>>({});

    // Sede state
    const [sedeDialogOpen, setSedeDialogOpen] = useState(false);
    const [selectedCompany, setSelectedCompany] = useState<CompanyConfig | null>(null);
    const [sedes, setSedes] = useState<Sede[]>([]);
    const [sedeLoading, setSedeLoading] = useState(false);
    const [importLoading, setImportLoading] = useState(false);
    const [validationLoading, setValidationLoading] = useState(false);
    const [validationResults, setValidationResults] = useState<ConnectionValidationResult[]>([]);

    useEffect(() => {
        loadConfigs();
    }, []);

    const loadConfigs = async () => {
        try {
            const response = await api.get('/admin/company-configs');
            setConfigs(response.data);
        } catch (error) {
            console.error(error);
        }
    };

    const handleSave = async () => {
        try {
            if (isEditing && currentConfig.id) {
                await api.put(`/admin/company-configs/${currentConfig.id}`, currentConfig as UpdateCompanyConfigRequest);
            } else {
                await api.post('/admin/company-configs', currentConfig as CreateCompanyConfigRequest);
            }
            setIsOpen(false);
            loadConfigs();
        } catch (error) {
            console.error(error);
            showError('Failed to save');
        }
    };

    const handleDelete = async (id: number) => {
        const result = await showConfirm('Are you sure?');
        if (!result.isConfirmed) return;
        try {
            await api.delete(`/admin/company-configs/${id}`);
            loadConfigs();
        } catch (error) {
            console.error(error);
        }
    };

    const handleValidateConnections = async () => {
        setValidationLoading(true);
        try {
            const response = await api.get('/admin/company-configs/validate-connections');
            setValidationResults(response.data);
        } catch (error) {
            console.error(error);
            showError('No se pudieron validar los connection strings.');
        } finally {
            setValidationLoading(false);
        }
    };

    const openCreate = () => {
        setCurrentConfig({ isActive: true, timeZoneId: 'SA Pacific Standard Time', isPilotMode: false, pilotSections: [] });
        setIsEditing(false);
        setIsOpen(true);
    };

    const openEdit = (config: CompanyConfig) => {
        setCurrentConfig({
            ...config,
            smartConnectionString: '',
            graphClientSecretRef: ''
        });
        setIsEditing(true);
        setIsOpen(true);
    };

    // Sede handlers
    const openSedes = async (config: CompanyConfig) => {
        setSelectedCompany(config);
        setSedeDialogOpen(true);
        setSedeLoading(true);
        try {
            const response = await api.get(`/admin/sedes/${config.id}`);
            setSedes(response.data);
        } catch (error) {
            console.error(error);
        } finally {
            setSedeLoading(false);
        }
    };

    const handleImportSedes = async () => {
        if (!selectedCompany) return;
        setImportLoading(true);
        try {
            const response = await api.post(`/admin/sedes/import/${selectedCompany.id}`);
            setSedes(response.data);
        } catch (error) {
            console.error(error);
            showError('Failed to import sedes. Make sure the API is connected to Smart DB.');
        } finally {
            setImportLoading(false);
        }
    };

    const handleToggleSede = async (sede: Sede) => {
        try {
            await api.patch(`/admin/sedes/${sede.id}/toggle`, { isActive: !sede.isActive });
            setSedes(prev => prev.map(s => s.id === sede.id ? { ...s, isActive: !s.isActive } : s));
        } catch (error) {
            console.error(error);
        }
    };

    const handleDeleteSede = async (id: number) => {
        const result = await showConfirm('¿Eliminar esta sede?');
        if (!result.isConfirmed) return;
        try {
            await api.delete(`/admin/sedes/${id}`);
            setSedes(prev => prev.filter(s => s.id !== id));
        } catch (error) {
            console.error(error);
        }
    };

    const activeSedes = sedes.filter(s => s.isActive);

    return (
        <div style={{ padding: '40px', maxWidth: '1200px', margin: '0 auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
                <div>
                    <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 600 }}>Configuraciones de Empresa</h1>
                    <p style={{ margin: '4px 0 0', color: '#666' }}>Gestiona las configuraciones de tenant para el sistema de aprovisionamiento.</p>
                </div>
                <div style={{ display: 'flex', gap: '12px' }}>
                    <Button
                        appearance="secondary"
                        icon={validationLoading ? <Spinner size="tiny" /> : <ArrowSyncRegular />}
                        onClick={handleValidateConnections}
                        disabled={validationLoading}
                    >
                        {validationLoading ? 'Validando...' : 'Validar conexiones'}
                    </Button>
                    <Button appearance="primary" icon={<AddRegular />} onClick={openCreate}>Agregar Configuración</Button>
                </div>
            </div>

            {validationResults.length > 0 && (
                <div style={{
                    background: 'white',
                    borderRadius: '8px',
                    boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
                    padding: '16px 20px',
                    marginBottom: '20px'
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                        <div>
                            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700 }}>Validación de connection strings</h3>
                            <p style={{ margin: '4px 0 0', color: '#666', fontSize: '13px' }}>
                                Se intenta descifrar cada `SmartConnectionString` con la clave del proceso actual.
                            </p>
                        </div>
                        <div style={{ display: 'flex', gap: '8px' }}>
                            <Badge appearance="filled" color="success">
                                {validationResults.filter(item => item.isValid).length} válidos
                            </Badge>
                            <Badge appearance="filled" color="danger">
                                {validationResults.filter(item => !item.isValid).length} inválidos
                            </Badge>
                        </div>
                    </div>

                    <div style={{ display: 'grid', gap: '10px' }}>
                        {validationResults.map(result => (
                            <div
                                key={result.id}
                                style={{
                                    border: `1px solid ${result.isValid ? '#b7ebc6' : '#ffd6d6'}`,
                                    background: result.isValid ? '#f6ffed' : '#fff5f5',
                                    borderRadius: '8px',
                                    padding: '12px 14px'
                                }}
                            >
                                <div style={{ display: 'flex', justifyContent: 'space-between', gap: '12px', alignItems: 'center' }}>
                                    <div>
                                        <div style={{ fontWeight: 700 }}>{result.displayName} <span style={{ color: '#666', fontWeight: 500 }}>({result.companyKey})</span></div>
                                        {!result.isValid && (
                                            <div style={{ marginTop: '4px', fontSize: '12px', color: '#a4262c' }}>
                                                {result.errorMessage || 'No se pudo descifrar o interpretar el connection string.'}
                                            </div>
                                        )}
                                    </div>
                                    <Badge appearance="filled" color={result.isValid ? 'success' : 'danger'}>
                                        {result.isValid ? 'Válido' : 'Inválido'}
                                    </Badge>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div style={{ background: 'white', borderRadius: '8px', boxShadow: '0 2px 8px rgba(0,0,0,0.1)', padding: '20px', overflowX: 'auto' }}>
                <Table style={{ tableLayout: 'fixed', width: '100%' }}>
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell style={{ width: '40px' }}>ID</TableHeaderCell>
                            <TableHeaderCell style={{ width: '110px' }}>Clave</TableHeaderCell>
                            <TableHeaderCell style={{ width: '120px' }}>Nombre</TableHeaderCell>
                            <TableHeaderCell style={{ width: '180px' }}>Front Host</TableHeaderCell>
                            <TableHeaderCell style={{ width: '200px' }}>API Host</TableHeaderCell>
                            <TableHeaderCell style={{ width: '140px' }}>SPA Tenant</TableHeaderCell>
                            <TableHeaderCell style={{ width: '70px' }}>Estado</TableHeaderCell>
                            <TableHeaderCell style={{ width: '120px' }}>Acciones</TableHeaderCell>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {configs.map(c => {
                            const cellStyle: React.CSSProperties = { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' };
                            return (
                                <TableRow key={c.id}>
                                    <TableCell>{c.id}</TableCell>
                                    <TableCell style={cellStyle}>{c.companyKey}</TableCell>
                                    <TableCell style={cellStyle}><b>{c.displayName}</b></TableCell>
                                    <TableCell style={cellStyle} title={c.frontHost}><span style={{ fontSize: '12px' }}>{c.frontHost}</span></TableCell>
                                    <TableCell style={cellStyle} title={c.apiHost}><span style={{ fontSize: '12px' }}>{c.apiHost}</span></TableCell>
                                    <TableCell style={cellStyle} title={c.spaTenantId}><span style={{ fontSize: '11px' }}>{c.spaTenantId}</span></TableCell>
                                    <TableCell>
                                        <span style={{
                                            padding: '4px 8px',
                                            borderRadius: '12px',
                                            background: c.isActive ? '#e6f7e9' : '#fbeaea',
                                            color: c.isActive ? '#107c10' : '#c50f1f',
                                            fontSize: '12px',
                                            fontWeight: 600
                                        }}>
                                            {c.isActive ? 'Activo' : 'Inactivo'}
                                        </span>
                                    </TableCell>
                                    <TableCell>
                                        <div style={{ display: 'flex', gap: '4px' }}>
                                            <Tooltip content="Gestionar Sedes" relationship="label">
                                                <Button icon={<BuildingRegular />} appearance="subtle" size="small" onClick={() => openSedes(c)} />
                                            </Tooltip>
                                            <Button icon={<EditRegular />} size="small" onClick={() => openEdit(c)} />
                                            <Button icon={<DeleteRegular />} appearance="subtle" size="small" onClick={() => handleDelete(c.id)} />
                                        </div>
                                    </TableCell>
                                </TableRow>
                            );
                        })}
                    </TableBody>
                </Table>
            </div>

            {/* Company Config Dialog */}
            <Dialog open={isOpen} onOpenChange={(_, data) => setIsOpen(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{isEditing ? 'Editar Configuración' : 'Nueva Configuración'}</DialogTitle>
                        <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '10px' }}>
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label required>Clave de Empresa</Label>
                                    <Input value={currentConfig.companyKey || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, companyKey: d.value })} placeholder="ej. zegel" />
                                </div>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label required>Nombre Distintivo</Label>
                                    <Input value={currentConfig.displayName || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, displayName: d.value })} placeholder="ej. Zegel" />
                                </div>
                            </div>

                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label>Front Host</Label>
                                    <Input value={currentConfig.frontHost || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, frontHost: d.value })} placeholder="teams.example.com" />
                                </div>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label>API Host</Label>
                                    <Input value={currentConfig.apiHost || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, apiHost: d.value })} placeholder="api.teams.example.com" />
                                </div>
                            </div>

                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label>SPA Client ID (Frontend)</Label>
                                    <Input value={currentConfig.spaClientId || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, spaClientId: d.value })} placeholder="Client ID for React App" />
                                </div>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label>SPA Tenant ID (Frontend)</Label>
                                    <Input value={currentConfig.spaTenantId || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, spaTenantId: d.value })} placeholder="Tenant ID for React App" />
                                </div>
                            </div>
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '16px' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label>Cadena de Conexión Smart</Label>
                                    <Input
                                        value={currentConfig.smartConnectionString || ''}
                                        type="password"
                                        placeholder={isEditing ? 'Dejar vacío para conservar el valor actual' : 'Server=...;Database=...;User Id=...;Password=...;'}
                                        onChange={(_, d) => setCurrentConfig({ ...currentConfig, smartConnectionString: d.value })}
                                    />
                                    {isEditing && (
                                        <p style={{ margin: '4px 0 0', fontSize: '11px', color: '#666' }}>
                                            Solo completa este campo si quieres reemplazar el connection string actual.
                                        </p>
                                    )}
                                </div>
                            </div>

                            <div style={{ padding: '10px', background: '#f5f5f5', borderRadius: '4px' }}>
                                <h4 style={{ margin: '0 0 10px 0' }}>Configuración de Graph API</h4>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                        <Label>Tenant ID</Label>
                                        <Input value={currentConfig.graphTenantId || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, graphTenantId: d.value })} />
                                    </div>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                        <Label>Client ID</Label>
                                        <Input value={currentConfig.graphClientId || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, graphClientId: d.value })} />
                                    </div>
                                </div>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', marginTop: '10px' }}>
                                    <Label>Referencia Client Secret</Label>
                                    <Input
                                        value={currentConfig.graphClientSecretRef || ''}
                                        type="password"
                                        placeholder={isEditing ? 'Dejar vacío para conservar el valor actual' : 'Referencia o valor configurado'}
                                        onChange={(_, d) => setCurrentConfig({ ...currentConfig, graphClientSecretRef: d.value })}
                                    />
                                    {isEditing && (
                                        <p style={{ margin: '4px 0 0', fontSize: '11px', color: '#666' }}>
                                            Solo completa este campo si quieres reemplazar el client secret actual.
                                        </p>
                                    )}
                                </div>
                            </div>

                            <div style={{ padding: '10px', background: '#e1f5fe', borderRadius: '4px', border: '1px solid #b3e5fc' }}>
                                <h4 style={{ margin: '0 0 10px 0', color: '#01579b' }}>Respaldo de Dominio Académico</h4>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label>Dominio Alternativo para Docentes</Label>
                                    <Input 
                                        value={currentConfig.teacherAltDomain || ''} 
                                        onChange={(_, d) => setCurrentConfig({ ...currentConfig, teacherAltDomain: d.value })} 
                                        placeholder="ej. zegel.pe" 
                                    />
                                    <p style={{ margin: '4px 0 0', fontSize: '11px', color: '#0277bd' }}>
                                        Utilizado como respaldo si el correo institucional principal no se encuentra en Azure AD.
                                    </p>
                                </div>
                            </div>

                            <div style={{ padding: '10px', background: '#fff3cd', borderRadius: '4px', border: '1px solid #ffeeba' }}>
                                <h4 style={{ margin: '0 0 10px 0', color: '#856404' }}>Configuración de Modo Piloto</h4>
                                <Switch 
                                    label={currentConfig.isPilotMode ? "Modo Piloto Habilitado" : "Modo Piloto Deshabilitado"} 
                                    checked={currentConfig.isPilotMode || false} 
                                    onChange={(_, d) => setCurrentConfig({ ...currentConfig, isPilotMode: d.checked })} 
                                />
                                {currentConfig.isPilotMode && (
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', marginTop: '10px' }}>
                                        <Label>IDs de Sección Permitidos (Separados por coma)</Label>
                                        <Input 
                                            value={currentConfig.pilotSections?.join(', ') || ''} 
                                            onChange={(_, d) => {
                                                const vals = d.value.split(',').map(s => parseInt(s.trim())).filter(n => !isNaN(n));
                                                setCurrentConfig({ ...currentConfig, pilotSections: vals });
                                            }} 
                                            placeholder="ej. 416734, 415868" 
                                        />
                                    </div>
                                )}
                            </div>

                            <div>
                                <Switch label={currentConfig.isActive ? "Activo" : "Inactivo"} checked={currentConfig.isActive} onChange={(_, d) => setCurrentConfig({ ...currentConfig, isActive: d.checked })} />
                            </div>
                        </DialogContent>
                         <DialogActions>
                            <Button appearance="secondary" onClick={() => setIsOpen(false)}>Cancelar</Button>
                            <Button appearance="primary" onClick={handleSave}>Guardar Cambios</Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {/* Sedes Dialog */}
            <Dialog open={sedeDialogOpen} onOpenChange={(_, data) => setSedeDialogOpen(data.open)}>
                <DialogSurface style={{ maxWidth: '720px' }}>
                    <DialogBody>
                        <DialogTitle>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                <BuildingRegular style={{ fontSize: '20px' }} />
                                <span>Sedes — {selectedCompany?.displayName}</span>
                            </div>
                        </DialogTitle>
                        <DialogContent style={{ marginTop: '10px' }}>
                            {/* Header actions */}
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    <Badge appearance="filled" color="success">{activeSedes.length} activas</Badge>
                                    <Badge appearance="filled" color="informative">{sedes.length} total</Badge>
                                </div>
                                <Button
                                    appearance="primary"
                                    icon={importLoading ? <Spinner size="tiny" /> : <ArrowSyncRegular />}
                                    disabled={importLoading}
                                    onClick={handleImportSedes}
                                >
                                    {importLoading ? 'Importando...' : 'Importar desde Smart'}
                                </Button>
                            </div>

                            {/* Sedes table */}
                            {sedeLoading ? (
                                <div style={{ display: 'flex', justifyContent: 'center', padding: '40px' }}>
                                    <Spinner label="Cargando sedes..." />
                                </div>
                            ) : sedes.length === 0 ? (
                                <div style={{
                                    padding: '40px',
                                    textAlign: 'center',
                                    background: '#fafafa',
                                    borderRadius: '8px',
                                    border: '1px dashed #ddd'
                                }}>
                                    <BuildingRegular style={{ fontSize: '32px', color: '#999' }} />
                                    <p style={{ color: '#666', marginTop: '8px' }}>
                                        No hay sedes importadas para esta empresa.
                                    </p>
                                    <p style={{ color: '#999', fontSize: '13px' }}>
                                        Haz clic en "Importar desde Smart" para cargar las sedes.
                                    </p>
                                </div>
                            ) : (
                                <div style={{ maxHeight: '400px', overflowY: 'auto' }}>
                                    <Table size="small">
                                        <TableHeader>
                                            <TableRow>
                                                <TableHeaderCell>ID Sede</TableHeaderCell>
                                                <TableHeaderCell>Código</TableHeaderCell>
                                                <TableHeaderCell>Nombre</TableHeaderCell>
                                                <TableHeaderCell>Estado</TableHeaderCell>
                                                <TableHeaderCell>Acciones</TableHeaderCell>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {sedes.map(s => (
                                                <TableRow key={s.id}>
                                                    <TableCell>{s.idSede}</TableCell>
                                                    <TableCell>
                                                        <code style={{
                                                            padding: '2px 6px',
                                                            background: '#f0f0f0',
                                                            borderRadius: '4px',
                                                            fontWeight: 600,
                                                            fontSize: '13px'
                                                        }}>
                                                            {s.codigo}
                                                        </code>
                                                    </TableCell>
                                                    <TableCell>{s.nombre}</TableCell>
                                                    <TableCell>
                                                        <span style={{
                                                            padding: '3px 8px',
                                                            borderRadius: '12px',
                                                            background: s.isActive ? '#e6f7e9' : '#fbeaea',
                                                            color: s.isActive ? '#107c10' : '#c50f1f',
                                                            fontSize: '11px',
                                                            fontWeight: 600
                                                        }}>
                                                            {s.isActive ? 'Activa' : 'Inactiva'}
                                                        </span>
                                                    </TableCell>
                                                    <TableCell>
                                                        <div style={{ display: 'flex', gap: '4px' }}>
                                                            <Switch
                                                                checked={s.isActive}
                                                                onChange={() => handleToggleSede(s)}
                                                                style={{ margin: 0 }}
                                                            />
                                                            <Button
                                                                icon={<DeleteRegular />}
                                                                appearance="subtle"
                                                                size="small"
                                                                onClick={() => handleDeleteSede(s.id)}
                                                            />
                                                        </div>
                                                    </TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                </div>
                            )}

                            {/* Active codes summary */}
                            {activeSedes.length > 0 && (
                                <div style={{
                                    marginTop: '16px',
                                    padding: '12px',
                                    background: '#f0f6ff',
                                    borderRadius: '6px',
                                    border: '1px solid #c7deff'
                                }}>
                                    <div style={{ fontSize: '12px', color: '#0f6cbd', fontWeight: 600, marginBottom: '4px' }}>
                                        Códigos activos para sincronización:
                                    </div>
                                    <code style={{ fontSize: '13px', color: '#333' }}>
                                        {activeSedes.map(s => s.codigo).join(', ')}
                                    </code>
                                </div>
                            )}
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={() => setSedeDialogOpen(false)}>Cerrar</Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default CompanyConfigsPage;
