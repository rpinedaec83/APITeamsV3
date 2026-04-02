import React, { useEffect, useState } from 'react';
import type { SyncSchedule, CreateSyncScheduleRequest } from '../types/SyncSchedule';
import type { CompanyConfig } from '../types/CompanyConfig';
import { useApiClient } from '../hooks/useApiClient';
import {
    Button,
    Dialog,
    DialogSurface,
    DialogBody,
    DialogTitle,
    DialogContent,
    DialogActions,
    Label,
    Switch,
    Select,
    Badge,
    Spinner,
    Tooltip,
} from '@fluentui/react-components';
import { DeleteRegular, AddRegular, EditRegular, CalendarClockRegular } from '@fluentui/react-icons';
import { showError, showConfirm } from '../utils/alerts';

const ALL_DAYS = [
    { key: 'Mon', label: 'Lun' },
    { key: 'Tue', label: 'Mar' },
    { key: 'Wed', label: 'Mié' },
    { key: 'Thu', label: 'Jue' },
    { key: 'Fri', label: 'Vie' },
    { key: 'Sat', label: 'Sáb' },
    { key: 'Sun', label: 'Dom' },
];

const SchedulesPage: React.FC = () => {
    const api = useApiClient();
    const [schedules, setSchedules] = useState<SyncSchedule[]>([]);
    const [companies, setCompanies] = useState<CompanyConfig[]>([]);
    const [loading, setLoading] = useState(true);

    // Dialog state
    const [dialogOpen, setDialogOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [editId, setEditId] = useState<number | null>(null);

    // Form state
    const [formCompanyId, setFormCompanyId] = useState<number>(0);
    const [formDays, setFormDays] = useState<string[]>([]);
    const [formHour, setFormHour] = useState(2);
    const [formMinute, setFormMinute] = useState(0);
    const [formEnabled, setFormEnabled] = useState(true);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        try {
            const [schedulesRes, companiesRes, configRes] = await Promise.all([
                api.get('/admin/sync-schedules'),
                api.get('/admin/company-configs'),
                api.get('/config')
            ]);
            
            setSchedules(schedulesRes.data);
            
            let fetchedCompanies = companiesRes.data || [];
            
            // If the admin list is empty, but we have current tenant info, use it!
            if (fetchedCompanies.length === 0 && configRes.data?.companyKey) {
                fetchedCompanies = [{
                    id: configRes.data.companyId,
                    companyKey: configRes.data.companyKey,
                    displayName: configRes.data.displayName || configRes.data.companyKey,
                    // Fill other fields with dummies if needed, but mainly we need id and displayName
                } as CompanyConfig];
            }

            setCompanies(fetchedCompanies);
            
            // Auto-select if there's only one company
            if (fetchedCompanies.length === 1) {
                setFormCompanyId(fetchedCompanies[0].id);
            }
        } catch (error) {
            console.error(error);
        } finally {
            setLoading(false);
        }
    };

    const openCreate = () => {
        setIsEditing(false);
        setEditId(null);
        setFormCompanyId(companies[0]?.id || 0);
        setFormDays(['Mon', 'Tue', 'Wed', 'Thu', 'Fri']);
        setFormHour(2);
        setFormMinute(0);
        setFormEnabled(true);
        setDialogOpen(true);
    };

    const openEdit = (s: SyncSchedule) => {
        setIsEditing(true);
        setEditId(s.id);
        setFormCompanyId(s.companyConfigId);
        setFormDays(s.daysOfWeek.split(',').filter(Boolean));
        setFormHour(s.hour);
        setFormMinute(s.minute);
        setFormEnabled(s.isEnabled);
        setDialogOpen(true);
    };

    const handleSave = async () => {
        const data: CreateSyncScheduleRequest = {
            companyConfigId: formCompanyId,
            daysOfWeek: formDays.join(','),
            hour: formHour,
            minute: formMinute,
            isEnabled: formEnabled,
        };

        try {
            if (isEditing && editId != null) {
                await api.put(`/admin/sync-schedules/${editId}`, { ...data, id: editId });
            } else {
                await api.post('/admin/sync-schedules', data);
            }
            setDialogOpen(false);
            loadData();
        } catch (error) {
            console.error(error);
            showError('Error al guardar');
        }
    };

    const handleDelete = async (id: number) => {
        const result = await showConfirm('¿Eliminar esta programación?');
        if (!result.isConfirmed) return;
        try {
            await api.delete(`/admin/sync-schedules/${id}`);
            loadData();
        } catch (error) {
            console.error(error);
        }
    };

    const handleToggle = async (s: SyncSchedule) => {
        try {
            await api.patch(`/admin/sync-schedules/${s.id}/toggle`, { isEnabled: !s.isEnabled });
            setSchedules(prev => prev.map(item => item.id === s.id ? { ...item, isEnabled: !item.isEnabled } : item));
        } catch (error) {
            console.error(error);
        }
    };

    const toggleDay = (day: string) => {
        setFormDays(prev => prev.includes(day) ? prev.filter(d => d !== day) : [...prev, day]);
    };

    const formatTime = (h: number, m: number) => {
        return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`;
    };

    const formatDays = (daysStr: string) => {
        const days = daysStr.split(',');
        return days.map(d => {
            const found = ALL_DAYS.find(ad => ad.key === d);
            return found?.label || d;
        }).join(', ');
    };

    const formatLastRun = (lastRunAt: string | null) => {
        if (!lastRunAt) return '—';
        const date = new Date(lastRunAt);
        return date.toLocaleString('es-PE', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
    };

    return (
        <div style={{ padding: '40px', maxWidth: '1000px', margin: '0 auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
                <div>
                    <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <CalendarClockRegular style={{ fontSize: '28px' }} />
                        Programación de Sincronización
                    </h1>
                    <p style={{ margin: '4px 0 0', color: '#666' }}>
                        Configura los días y horas para la sincronización completa de Teams.
                    </p>
                </div>
                <Button appearance="primary" icon={<AddRegular />} onClick={openCreate}>
                    Nueva Programación
                </Button>
            </div>

            {loading ? (
                <div style={{ display: 'flex', justifyContent: 'center', padding: '60px' }}>
                    <Spinner label="Cargando programaciones..." />
                </div>
            ) : schedules.length === 0 ? (
                <div style={{
                    background: 'white',
                    borderRadius: '12px',
                    padding: '60px',
                    textAlign: 'center',
                    boxShadow: '0 2px 8px rgba(0,0,0,0.08)'
                }}>
                    <CalendarClockRegular style={{ fontSize: '48px', color: '#999' }} />
                    <h3 style={{ color: '#555', margin: '16px 0 8px' }}>No hay programaciones configuradas</h3>
                    <p style={{ color: '#999' }}>Haz clic en "Nueva Programación" para empezar.</p>
                </div>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                    {schedules.map(s => (
                        <div key={s.id} style={{
                            background: 'white',
                            borderRadius: '12px',
                            padding: '20px 24px',
                            boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
                            border: `2px solid ${s.isEnabled ? '#e6f7e9' : '#f0f0f0'}`,
                            opacity: s.isEnabled ? 1 : 0.7,
                            transition: 'all 0.2s'
                        }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                                {/* Left: info */}
                                <div style={{ flex: 1 }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '12px' }}>
                                        <span style={{ fontSize: '18px', fontWeight: 700 }}>
                                            {s.companyName}
                                        </span>
                                        <Badge
                                            appearance="filled"
                                            color={s.isEnabled ? 'success' : 'danger'}
                                        >
                                            {s.isEnabled ? 'Activo' : 'Inactivo'}
                                        </Badge>
                                    </div>

                                    {/* Time */}
                                    <div style={{ display: 'flex', gap: '24px', flexWrap: 'wrap' }}>
                                        <div>
                                            <div style={{ fontSize: '11px', color: '#888', textTransform: 'uppercase', fontWeight: 600, marginBottom: '4px' }}>
                                                Hora
                                            </div>
                                            <span style={{
                                                fontSize: '28px',
                                                fontWeight: 700,
                                                fontFamily: 'monospace',
                                                color: '#0f6cbd',
                                                letterSpacing: '2px'
                                            }}>
                                                {formatTime(s.hour, s.minute)}
                                            </span>
                                        </div>

                                        <div>
                                            <div style={{ fontSize: '11px', color: '#888', textTransform: 'uppercase', fontWeight: 600, marginBottom: '4px' }}>
                                                Días
                                            </div>
                                            <div style={{ display: 'flex', gap: '4px', marginTop: '4px' }}>
                                                {ALL_DAYS.map(d => {
                                                    const active = s.daysOfWeek.split(',').includes(d.key);
                                                    return (
                                                        <span key={d.key} style={{
                                                            width: '36px',
                                                            height: '36px',
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center',
                                                            borderRadius: '50%',
                                                            fontSize: '11px',
                                                            fontWeight: 600,
                                                            background: active ? '#0f6cbd' : '#f0f0f0',
                                                            color: active ? 'white' : '#999',
                                                            transition: 'all 0.2s'
                                                        }}>
                                                            {d.label}
                                                        </span>
                                                    );
                                                })}
                                            </div>
                                        </div>

                                        <div>
                                            <div style={{ fontSize: '11px', color: '#888', textTransform: 'uppercase', fontWeight: 600, marginBottom: '4px' }}>
                                                Última ejecución
                                            </div>
                                            <span style={{ fontSize: '13px', color: '#555' }}>
                                                {formatLastRun(s.lastRunAt)}
                                            </span>
                                        </div>
                                    </div>
                                </div>

                                {/* Right: actions */}
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    <Switch
                                        checked={s.isEnabled}
                                        onChange={() => handleToggle(s)}
                                    />
                                    <Tooltip content="Editar" relationship="label">
                                        <Button icon={<EditRegular />} appearance="subtle" size="small" onClick={() => openEdit(s)} />
                                    </Tooltip>
                                    <Tooltip content="Eliminar" relationship="label">
                                        <Button icon={<DeleteRegular />} appearance="subtle" size="small" onClick={() => handleDelete(s.id)} />
                                    </Tooltip>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Create/Edit Dialog */}
            <Dialog open={dialogOpen} onOpenChange={(_, data) => setDialogOpen(data.open)}>
                <DialogSurface style={{ maxWidth: '500px' }}>
                    <DialogBody>
                        <DialogTitle>
                            {isEditing ? 'Editar Programación' : 'Nueva Programación'}
                        </DialogTitle>
                        <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '20px', marginTop: '12px' }}>
                            {/* Company */}
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                <Label required>Empresa</Label>
                                {companies.length === 1 ? (
                                    <div style={{ padding: '8px 12px', background: '#f5f5f5', borderRadius: '4px', border: '1px solid #ddd', fontWeight: 600 }}>
                                        {companies[0].displayName}
                                    </div>
                                ) : (
                                    <Select
                                        value={formCompanyId.toString()}
                                        onChange={(_, data) => setFormCompanyId(Number(data.value))}
                                    >
                                        <option value="0">Seleccionar empresa...</option>
                                        {companies.map(c => (
                                            <option key={c.id} value={c.id}>{c.displayName}</option>
                                        ))}
                                    </Select>
                                )}
                            </div>

                            {/* Days */}
                            <div>
                                <Label required style={{ marginBottom: '8px', display: 'block' }}>Días de la semana</Label>
                                <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                                    {ALL_DAYS.map(d => (
                                        <button
                                            key={d.key}
                                            type="button"
                                            onClick={() => toggleDay(d.key)}
                                            style={{
                                                width: '48px',
                                                height: '48px',
                                                borderRadius: '50%',
                                                border: `2px solid ${formDays.includes(d.key) ? '#0f6cbd' : '#ddd'}`,
                                                background: formDays.includes(d.key) ? '#0f6cbd' : 'white',
                                                color: formDays.includes(d.key) ? 'white' : '#666',
                                                fontWeight: 600,
                                                fontSize: '13px',
                                                cursor: 'pointer',
                                                transition: 'all 0.2s'
                                            }}
                                        >
                                            {d.label}
                                        </button>
                                    ))}
                                </div>
                            </div>

                            {/* Time */}
                            <div>
                                <Label required style={{ marginBottom: '8px', display: 'block' }}>Hora de ejecución</Label>
                                <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                                    <Select
                                        value={formHour.toString()}
                                        onChange={(_, data) => setFormHour(Number(data.value))}
                                        style={{ width: '100px' }}
                                    >
                                        {Array.from({ length: 24 }, (_, i) => (
                                            <option key={i} value={i}>{i.toString().padStart(2, '0')}</option>
                                        ))}
                                    </Select>
                                    <span style={{ fontSize: '24px', fontWeight: 700 }}>:</span>
                                    <Select
                                        value={formMinute.toString()}
                                        onChange={(_, data) => setFormMinute(Number(data.value))}
                                        style={{ width: '100px' }}
                                    >
                                        {[0, 15, 30, 45].map(m => (
                                            <option key={m} value={m}>{m.toString().padStart(2, '0')}</option>
                                        ))}
                                    </Select>
                                </div>
                            </div>

                            {/* Enabled */}
                            <div>
                                <Switch
                                    checked={formEnabled}
                                    onChange={(_, data) => setFormEnabled(data.checked)}
                                    label={formEnabled ? 'Habilitado' : 'Deshabilitado'}
                                />
                            </div>

                            {/* Preview */}
                            {formDays.length > 0 && (
                                <div style={{
                                    padding: '12px',
                                    background: '#f0f6ff',
                                    borderRadius: '6px',
                                    border: '1px solid #c7deff',
                                    fontSize: '13px'
                                }}>
                                    <strong>Vista previa: </strong>
                                    Ejecutar todos los <strong>{formatDays(formDays.join(','))}</strong> a las{' '}
                                    <strong>{formatTime(formHour, formMinute)}</strong>
                                </div>
                            )}
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={() => setDialogOpen(false)}>Cancelar</Button>
                            <Button
                                appearance="primary"
                                onClick={handleSave}
                                disabled={formDays.length === 0 || formCompanyId === 0}
                            >
                                {isEditing ? 'Guardar Cambios' : 'Crear Programación'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default SchedulesPage;
