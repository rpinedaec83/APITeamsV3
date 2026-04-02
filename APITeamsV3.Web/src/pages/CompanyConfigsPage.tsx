
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

    const openCreate = () => {
        setCurrentConfig({ isActive: true, timeZoneId: 'SA Pacific Standard Time' });
        setIsEditing(false);
        setIsOpen(true);
    };

    const openEdit = (config: CompanyConfig) => {
        setCurrentConfig({ ...config });
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
                    <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 600 }}>Company Configurations</h1>
                    <p style={{ margin: '4px 0 0', color: '#666' }}>Manage tenant configurations for the provisioning system.</p>
                </div>
                <Button appearance="primary" icon={<AddRegular />} onClick={openCreate}>Add Configuration</Button>
            </div>

            <div style={{ background: 'white', borderRadius: '8px', boxShadow: '0 2px 8px rgba(0,0,0,0.1)', padding: '20px', overflowX: 'auto' }}>
                <Table style={{ tableLayout: 'fixed', width: '100%' }}>
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell style={{ width: '40px' }}>ID</TableHeaderCell>
                            <TableHeaderCell style={{ width: '110px' }}>Key</TableHeaderCell>
                            <TableHeaderCell style={{ width: '120px' }}>Name</TableHeaderCell>
                            <TableHeaderCell style={{ width: '180px' }}>Front Host</TableHeaderCell>
                            <TableHeaderCell style={{ width: '200px' }}>API Host</TableHeaderCell>
                            <TableHeaderCell style={{ width: '140px' }}>SPA Tenant</TableHeaderCell>
                            <TableHeaderCell style={{ width: '70px' }}>Active</TableHeaderCell>
                            <TableHeaderCell style={{ width: '120px' }}>Actions</TableHeaderCell>
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
                                            {c.isActive ? 'Active' : 'Inactive'}
                                        </span>
                                    </TableCell>
                                    <TableCell>
                                        <div style={{ display: 'flex', gap: '4px' }}>
                                            <Tooltip content="Manage Sedes" relationship="label">
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
                        <DialogTitle>{isEditing ? 'Edit Configuration' : 'New Configuration'}</DialogTitle>
                        <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '10px' }}>
                            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label required>Company Key</Label>
                                    <Input value={currentConfig.companyKey || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, companyKey: d.value })} placeholder="e.g. zegel" />
                                </div>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Label required>Display Name</Label>
                                    <Input value={currentConfig.displayName || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, displayName: d.value })} placeholder="e.g. Zegel" />
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
                                    <Label>Smart Connection String</Label>
                                    <Input value={currentConfig.smartConnectionString || ''} type="password" onChange={(_, d) => setCurrentConfig({ ...currentConfig, smartConnectionString: d.value })} />
                                </div>
                            </div>

                            <div style={{ padding: '10px', background: '#f5f5f5', borderRadius: '4px' }}>
                                <h4 style={{ margin: '0 0 10px 0' }}>Graph API Settings</h4>
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
                                    <Label>Client Secret Ref</Label>
                                    <Input value={currentConfig.graphClientSecretRef || ''} onChange={(_, d) => setCurrentConfig({ ...currentConfig, graphClientSecretRef: d.value })} />
                                </div>
                            </div>

                            <div>
                                <Switch label={currentConfig.isActive ? "Active" : "Inactive"} checked={currentConfig.isActive} onChange={(_, d) => setCurrentConfig({ ...currentConfig, isActive: d.checked })} />
                            </div>
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={() => setIsOpen(false)}>Cancel</Button>
                            <Button appearance="primary" onClick={handleSave}>Save Changes</Button>
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
