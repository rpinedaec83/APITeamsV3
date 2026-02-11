
import React, { useEffect, useState } from 'react';
import type { CompanyConfig, CreateCompanyConfigRequest, UpdateCompanyConfigRequest } from '../types/CompanyConfig';
import { CompanyConfigService } from '../services/CompanyConfigService';
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
    Switch
} from '@fluentui/react-components';
import { DeleteRegular, EditRegular, AddRegular } from '@fluentui/react-icons';

const CompanyConfigsPage: React.FC = () => {
    const [configs, setConfigs] = useState<CompanyConfig[]>([]);
    const [isOpen, setIsOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [currentConfig, setCurrentConfig] = useState<Partial<CompanyConfig>>({});

    useEffect(() => {
        loadConfigs();
    }, []);

    const loadConfigs = async () => {
        try {
            const data = await CompanyConfigService.getAll();
            setConfigs(data);
        } catch (error) {
            console.error(error);
        }
    };

    const handleSave = async () => {
        try {
            if (isEditing && currentConfig.id) {
                await CompanyConfigService.update(currentConfig.id, currentConfig as UpdateCompanyConfigRequest);
            } else {
                await CompanyConfigService.create(currentConfig as CreateCompanyConfigRequest);
            }
            setIsOpen(false);
            loadConfigs();
        } catch (error) {
            console.error(error);
            alert('Failed to save');
        }
    };

    const handleDelete = async (id: number) => {
        if (!confirm('Are you sure?')) return;
        try {
            await CompanyConfigService.delete(id);
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

    return (
        <div style={{ padding: '40px', maxWidth: '1200px', margin: '0 auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
                <div>
                    <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 600 }}>Company Configurations</h1>
                    <p style={{ margin: '4px 0 0', color: '#666' }}>Manage tenant configurations for the provisioning system.</p>
                </div>
                <Button appearance="primary" icon={<AddRegular />} onClick={openCreate}>Add Configuration</Button>
            </div>

            <div style={{ background: 'white', borderRadius: '8px', boxShadow: '0 2px 8px rgba(0,0,0,0.1)', padding: '20px' }}>
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHeaderCell>ID</TableHeaderCell>
                            <TableHeaderCell>Key</TableHeaderCell>
                            <TableHeaderCell>Name</TableHeaderCell>
                            <TableHeaderCell>Front Host</TableHeaderCell>
                            <TableHeaderCell>API Host</TableHeaderCell>
                            <TableHeaderCell>Active</TableHeaderCell>
                            <TableHeaderCell>Actions</TableHeaderCell>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {configs.map(c => (
                            <TableRow key={c.id}>
                                <TableCell>{c.id}</TableCell>
                                <TableCell>{c.companyKey}</TableCell>
                                <TableCell><b>{c.displayName}</b></TableCell>
                                <TableCell>{c.frontHost}</TableCell>
                                <TableCell>{c.apiHost}</TableCell>
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
                                    <div style={{ display: 'flex', gap: '8px' }}>
                                        <Button icon={<EditRegular />} onClick={() => openEdit(c)} />
                                        <Button icon={<DeleteRegular />} appearance="subtle" onClick={() => handleDelete(c.id)} />
                                    </div>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </div>

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
        </div>
    );
};

export default CompanyConfigsPage;
