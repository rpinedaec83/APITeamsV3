import React, { useEffect, useMemo, useState } from 'react';
import type { SyncSchedule, CreateSyncScheduleRequest } from '../types/SyncSchedule';
import type { CompanyConfig } from '../types/CompanyConfig';
import { useApiClient } from '../hooks/useApiClient';
import {
    Badge,
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Input,
    Label,
    Select,
    Spinner,
    Switch,
    Text,
    Title1,
    Title3,
    makeStyles,
    shorthands,
    tokens,
} from '@fluentui/react-components';
import {
    AddRegular,
    BuildingRegular,
    CalendarClockRegular,
    CheckmarkCircleRegular,
    ClockRegular,
    DeleteRegular,
    EditRegular,
} from '@fluentui/react-icons';
import { useNavigate } from 'react-router-dom';
import { showConfirm, showError } from '../utils/alerts';

const ALL_DAYS = [
    { key: 'Mon', label: 'Lun' },
    { key: 'Tue', label: 'Mar' },
    { key: 'Wed', label: 'Mié' },
    { key: 'Thu', label: 'Jue' },
    { key: 'Fri', label: 'Vie' },
    { key: 'Sat', label: 'Sáb' },
    { key: 'Sun', label: 'Dom' },
];

const WINDOWS_TO_IANA_TIMEZONES: Record<string, string> = {
    'SA Pacific Standard Time': 'America/Lima',
    'Pacific SA Standard Time': 'America/Santiago',
    'Eastern Standard Time': 'America/New_York',
    'SA Western Standard Time': 'America/La_Paz',
    UTC: 'UTC',
};

const useStyles = makeStyles({
    root: {
        minHeight: '100%',
        padding: '28px',
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        background: 'linear-gradient(180deg, #eef2f7 0%, #e6ecf5 100%)',
    },
    hero: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1.5fr) minmax(320px, 0.95fr)',
        gap: '18px',
        '@media (max-width: 1100px)': {
            gridTemplateColumns: '1fr',
        },
    },
    heroPanel: {
        color: '#fff',
        background: 'linear-gradient(135deg, #1f2d3d 0%, #28445f 52%, #336e8b 100%)',
        ...shorthands.borderRadius('20px'),
        ...shorthands.padding('24px'),
        boxShadow: '0 18px 36px rgba(31, 45, 61, 0.24)',
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
    },
    eyebrow: {
        fontSize: tokens.fontSizeBase200,
        textTransform: 'uppercase',
        letterSpacing: '0.08em',
        color: 'rgba(255,255,255,0.72)',
        fontWeight: tokens.fontWeightSemibold,
    },
    heroStats: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
        gap: '12px',
        '@media (max-width: 800px)': {
            gridTemplateColumns: '1fr',
        },
    },
    heroStatCard: {
        backgroundColor: 'rgba(255,255,255,0.08)',
        border: '1px solid rgba(255,255,255,0.08)',
        ...shorthands.borderRadius('14px'),
        ...shorthands.padding('14px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    sidePanel: {
        backgroundColor: '#fff',
        ...shorthands.borderRadius('20px'),
        ...shorthands.padding('22px'),
        boxShadow: tokens.shadow8,
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
    },
    sideMetric: {
        backgroundColor: '#f7f9fc',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.borderRadius('14px'),
        ...shorthands.padding('14px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    card: {
        backgroundColor: '#fff',
        ...shorthands.borderRadius('20px'),
        boxShadow: tokens.shadow8,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
    },
    cardHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '14px',
        ...shorthands.padding('18px', '20px'),
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: '#f7f9fc',
    },
    cardBody: {
        ...shorthands.padding('20px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '18px',
    },
    filterRow: {
        display: 'grid',
        gridTemplateColumns: 'minmax(220px, 1.2fr) minmax(220px, 1fr) auto',
        gap: '12px',
        alignItems: 'end',
        '@media (max-width: 900px)': {
            gridTemplateColumns: '1fr',
        },
    },
    filterField: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    scheduleGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
        gap: '16px',
    },
    scheduleCard: {
        background: 'linear-gradient(180deg, #ffffff 0%, #fbfcfe 100%)',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.borderRadius('18px'),
        ...shorthands.padding('18px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        boxShadow: tokens.shadow4,
    },
    scheduleTop: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: '12px',
        alignItems: 'flex-start',
    },
    metricGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        gap: '12px',
    },
    metricCard: {
        backgroundColor: '#f7f9fc',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.borderRadius('14px'),
        ...shorthands.padding('12px'),
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    dayRow: {
        display: 'flex',
        gap: '6px',
        flexWrap: 'wrap',
    },
    dayPill: {
        minWidth: '40px',
        textAlign: 'center',
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        ...shorthands.padding('8px', '10px'),
        ...shorthands.borderRadius('999px'),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: '#f4f7fb',
        color: tokens.colorNeutralForeground2,
    },
    dayPillActive: {
        backgroundColor: '#0f6cbd',
        color: '#fff',
        border: '1px solid #0f6cbd',
    },
    actionRow: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'stretch',
        gap: '10px',
    },
    actionText: {
        display: 'block',
    },
    actionButtons: {
        display: 'flex',
        gap: '8px',
        alignItems: 'center',
        flexWrap: 'wrap',
        justifyContent: 'flex-start',
    },
    statusBadge: {
        whiteSpace: 'nowrap',
    },
    emptyState: {
        textAlign: 'center',
        ...shorthands.padding('40px', '20px'),
        color: tokens.colorNeutralForeground3,
    },
    daySelector: {
        display: 'flex',
        gap: '8px',
        flexWrap: 'wrap',
    },
    dayButton: {
        width: '52px',
        height: '52px',
        borderRadius: '999px',
        border: `2px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: '#fff',
        color: tokens.colorNeutralForeground2,
        fontWeight: tokens.fontWeightSemibold,
        cursor: 'pointer',
    },
    dayButtonActive: {
        backgroundColor: '#0f6cbd',
        color: '#fff',
        border: '2px solid #0f6cbd',
    },
    dialogGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        gap: '14px',
        '@media (max-width: 700px)': {
            gridTemplateColumns: '1fr',
        },
    },
    fullWidth: {
        gridColumn: '1 / -1',
    },
    previewBox: {
        backgroundColor: '#f0f6ff',
        border: '1px solid #c7deff',
        ...shorthands.borderRadius('12px'),
        ...shorthands.padding('12px'),
    },
});

const resolveBrowserTimeZone = (timeZoneId?: string) => WINDOWS_TO_IANA_TIMEZONES[timeZoneId ?? ''] ?? timeZoneId ?? 'America/Lima';
const formatTime = (hour: number, minute: number) => `${hour.toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`;
const formatDays = (daysStr: string) => daysStr.split(',').filter(Boolean).map(day => ALL_DAYS.find(item => item.key === day)?.label ?? day).join(', ');
const formatDateTime = (value: string | null | undefined, timeZoneId?: string) => {
    if (!value) return '—';
    const date = new Date(/z$|[+-]\d{2}:\d{2}$/i.test(value) ? value : `${value}Z`);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat('es-PE', {
        timeZone: resolveBrowserTimeZone(timeZoneId),
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: true,
    }).format(date);
};

const SchedulesPage: React.FC = () => {
    const api = useApiClient();
    const styles = useStyles();
    const navigate = useNavigate();
    const [schedules, setSchedules] = useState<SyncSchedule[]>([]);
    const [companies, setCompanies] = useState<CompanyConfig[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [dialogOpen, setDialogOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [editId, setEditId] = useState<number | null>(null);
    const [companyFilter, setCompanyFilter] = useState('');
    const [statusFilter, setStatusFilter] = useState<'all' | 'enabled' | 'disabled'>('all');
    const [formCompanyId, setFormCompanyId] = useState(0);
    const [formDays, setFormDays] = useState<string[]>([]);
    const [formHour, setFormHour] = useState(2);
    const [formMinute, setFormMinute] = useState(0);
    const [formEnabled, setFormEnabled] = useState(true);

    const loadData = async () => {
        setLoading(true);
        try {
            const [schedulesRes, companiesRes, configRes] = await Promise.all([
                api.get('/admin/sync-schedules'),
                api.get('/admin/company-configs'),
                api.get('/config'),
            ]);
            setSchedules(Array.isArray(schedulesRes.data) ? schedulesRes.data : []);
            let fetchedCompanies = Array.isArray(companiesRes.data) ? companiesRes.data : [];
            if (fetchedCompanies.length === 0 && configRes.data?.companyKey) {
                fetchedCompanies = [{
                    id: configRes.data.companyId,
                    companyKey: configRes.data.companyKey,
                    displayName: configRes.data.displayName || configRes.data.companyKey,
                    timeZoneId: configRes.data.timeZoneId || 'SA Pacific Standard Time',
                    frontHost: '',
                    apiHost: '',
                    graphTenantId: '',
                    graphClientId: '',
                    graphClientSecretRef: '',
                    defaultChannelName: '',
                    meetingPolicyMode: '',
                    isActive: true,
                    isPilotMode: false,
                    pilotSections: [],
                    isMaintenanceMode: false,
                    maintenanceMessage: '',
                } as CompanyConfig];
            }
            setCompanies(fetchedCompanies);
            if (fetchedCompanies.length === 1) setFormCompanyId(fetchedCompanies[0].id);
        } catch (error) {
            console.error(error);
            showError('No se pudieron cargar las programaciones.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void loadData();
    }, []);

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

    const openEdit = (schedule: SyncSchedule) => {
        setIsEditing(true);
        setEditId(schedule.id);
        setFormCompanyId(schedule.companyConfigId);
        setFormDays(schedule.daysOfWeek.split(',').filter(Boolean));
        setFormHour(schedule.hour);
        setFormMinute(schedule.minute);
        setFormEnabled(schedule.isEnabled);
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
        setSaving(true);
        try {
            if (isEditing && editId != null) await api.put(`/admin/sync-schedules/${editId}`, { ...data, id: editId });
            else await api.post('/admin/sync-schedules', data);
            setDialogOpen(false);
            await loadData();
        } catch (error) {
            console.error(error);
            showError('Error al guardar la programación.');
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async (id: number) => {
        const result = await showConfirm('¿Eliminar esta programación?');
        if (!result.isConfirmed) return;
        try {
            await api.delete(`/admin/sync-schedules/${id}`);
            await loadData();
        } catch (error) {
            console.error(error);
            showError('No se pudo eliminar la programación.');
        }
    };

    const handleToggle = async (schedule: SyncSchedule) => {
        try {
            await api.patch(`/admin/sync-schedules/${schedule.id}/toggle`, { isEnabled: !schedule.isEnabled });
            setSchedules(prev => prev.map(item => item.id === schedule.id ? { ...item, isEnabled: !item.isEnabled } : item));
        } catch (error) {
            console.error(error);
            showError('No se pudo actualizar el estado de la programación.');
        }
    };

    const filteredSchedules = useMemo(() => schedules.filter(schedule => {
        if (companyFilter && String(schedule.companyConfigId) !== companyFilter) return false;
        if (statusFilter === 'enabled' && !schedule.isEnabled) return false;
        if (statusFilter === 'disabled' && schedule.isEnabled) return false;
        return true;
    }), [companyFilter, schedules, statusFilter]);

    const enabledCount = schedules.filter(item => item.isEnabled).length;
    const uniqueCompanies = new Set(schedules.map(item => item.companyConfigId)).size;
    const lastRunCount = schedules.filter(item => item.lastRunAt).length;

    return (
        <div className={styles.root}>
            <div className={styles.hero}>
                <div className={styles.heroPanel}>
                    <div className={styles.eyebrow}>Synchronization Scheduling</div>
                    <Title1 style={{ color: '#fff', margin: 0 }}>Programación de Sincronización</Title1>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                         <Text style={{ color: 'rgba(255,255,255,0.82)', maxWidth: '64ch' }}>
                            Define ventanas operativas para la sincronización completa de Teams por empresa, con control visible de estado, horario y última ejecución real.
                        </Text>
                    </div>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px' }}>
                        <Badge appearance="filled" color="informative">Sincronización automatizada</Badge>
                        <Badge appearance="outline">Zona horaria por tenant</Badge>
                        <Badge appearance="outline">Control operativo central</Badge>
                    </div>
                    <div className={styles.heroStats}>
                        <div className={styles.heroStatCard}><Text size={700} weight="bold">{schedules.length}</Text><Text>Programaciones registradas</Text></div>
                        <div className={styles.heroStatCard}><Text size={700} weight="bold">{enabledCount}</Text><Text>Programaciones activas</Text></div>
                        <div className={styles.heroStatCard}><Text size={700} weight="bold">{uniqueCompanies}</Text><Text>Empresas con horario</Text></div>
                    </div>
                </div>
                <div className={styles.sidePanel}>
                    <Title3 style={{ margin: 0 }}>Lectura rápida</Title3>
                    <div className={styles.sideMetric}><Text size={200} weight="semibold">Últimos runs detectados</Text><Text size={700} weight="bold">{lastRunCount}</Text><Text>Programaciones con evidencia de ejecución.</Text></div>
                    <div className={styles.sideMetric}><Text size={200} weight="semibold">Horario sugerido</Text><Text size={600} weight="bold">02:00 a. m.</Text><Text>Ventana típica para minimizar fricción operativa.</Text></div>
                    <div className={styles.sideMetric}><Text size={200} weight="semibold">Vista</Text><Text>La hora y las fechas se muestran usando la zona horaria configurada del tenant.</Text></div>
                </div>
            </div>

            <div className={styles.card}>
                <div className={styles.cardHeader}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                        <Title3 style={{ margin: 0 }}>Programaciones registradas</Title3>
                        <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>Filtra, revisa y modifica las ventanas activas por empresa.</Text>
                    </div>
                    <Button appearance="primary" icon={<AddRegular />} onClick={openCreate}>Nueva programación</Button>
                </div>
                <div className={styles.cardBody}>
                    <div className={styles.filterRow}>
                        <div className={styles.filterField}>
                            <Label>Empresa</Label>
                            <Select value={companyFilter} onChange={(_, data) => setCompanyFilter(data.value)}>
                                <option value="">Todas</option>
                                {companies.map(company => <option key={company.id} value={String(company.id)}>{company.displayName}</option>)}
                            </Select>
                        </div>
                        <div className={styles.filterField}>
                            <Label>Estado</Label>
                            <Select value={statusFilter} onChange={(_, data) => setStatusFilter((data.value as 'all' | 'enabled' | 'disabled') || 'all')}>
                                <option value="all">Todas</option>
                                <option value="enabled">Solo activas</option>
                                <option value="disabled">Solo inactivas</option>
                            </Select>
                        </div>
                        <Button appearance="secondary" onClick={() => { setCompanyFilter(''); setStatusFilter('all'); }}>Limpiar filtros</Button>
                    </div>

                    {loading ? (
                        <div className={styles.emptyState}><Spinner label="Cargando programaciones..." /></div>
                    ) : filteredSchedules.length === 0 ? (
                        <div className={styles.emptyState}>
                            <CalendarClockRegular style={{ fontSize: '46px', opacity: 0.4, marginBottom: '12px' }} />
                            <div style={{ fontWeight: 700, marginBottom: '6px' }}>No hay programaciones visibles</div>
                            <div>Prueba con otros filtros o crea una nueva programación.</div>
                        </div>
                    ) : (
                        <div className={styles.scheduleGrid}>
                            {filteredSchedules.map(schedule => {
                                const activeDays = new Set(schedule.daysOfWeek.split(',').filter(Boolean));
                                return (
                                    <Card key={schedule.id} className={styles.scheduleCard}>
                                        <div className={styles.scheduleTop}>
                                            <div>
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
                                                    <Title3 style={{ margin: 0 }}>{schedule.companyName}</Title3>
                                                    <Badge appearance="filled" color={schedule.isEnabled ? 'success' : 'danger'}>{schedule.isEnabled ? 'Activa' : 'Inactiva'}</Badge>
                                                </div>
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '8px', color: tokens.colorNeutralForeground3 }}>
                                                    <BuildingRegular />
                                                    <Text size={200}>{schedule.timeZoneId || 'SA Pacific Standard Time'}</Text>
                                                </div>
                                            </div>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                <Switch checked={schedule.isEnabled} onChange={() => handleToggle(schedule)} />
                                                <Button appearance="subtle" icon={<EditRegular />} onClick={() => openEdit(schedule)} />
                                                <Button appearance="subtle" icon={<DeleteRegular />} onClick={() => handleDelete(schedule.id)} />
                                            </div>
                                        </div>
                                        <div className={styles.metricGrid}>
                                            <div className={styles.metricCard}><Text size={200} weight="semibold">Hora de ejecución</Text><Text size={700} weight="bold" font="monospace">{formatTime(schedule.hour, schedule.minute)}</Text></div>
                                            <div className={styles.metricCard}><Text size={200} weight="semibold">Última ejecución</Text><Text>{formatDateTime(schedule.lastRunAt, schedule.timeZoneId)}</Text></div>
                                            <div className={styles.metricCard}><Text size={200} weight="semibold">Creada el</Text><Text>{formatDateTime(schedule.createdAt, schedule.timeZoneId)}</Text></div>
                                            <div className={styles.metricCard}><Text size={200} weight="semibold">Cobertura semanal</Text><Text>{schedule.daysOfWeek.split(',').filter(Boolean).length} día(s)</Text></div>
                                        </div>
                                        <div>
                                            <Text size={200} weight="semibold">Días configurados</Text>
                                            <div className={styles.dayRow} style={{ marginTop: '10px' }}>
                                                {ALL_DAYS.map(day => <div key={day.key} className={`${styles.dayPill} ${activeDays.has(day.key) ? styles.dayPillActive : ''}`}>{day.label}</div>)}
                                            </div>
                                        </div>
                                        <div className={styles.actionRow}>
                                            <Text size={200} className={styles.actionText} style={{ color: tokens.colorNeutralForeground3 }}>
                                                Ejecuta cada {formatDays(schedule.daysOfWeek)} a las {formatTime(schedule.hour, schedule.minute)}.
                                            </Text>
                                            <div className={styles.actionButtons}>
                                                <Button appearance="secondary" size="small" onClick={() => navigate(`/schedules/${schedule.id}/executions`)}>
                                                    Ver ejecuciones
                                                </Button>
                                                {schedule.isEnabled
                                                    ? <Badge className={styles.statusBadge} appearance="outline" color="success" icon={<CheckmarkCircleRegular />}>Lista para ejecutar</Badge>
                                                    : <Badge className={styles.statusBadge} appearance="outline" color="danger">Pausada</Badge>}
                                            </div>
                                        </div>
                                    </Card>
                                );
                            })}
                        </div>
                    )}
                </div>
            </div>

            <Dialog open={dialogOpen} onOpenChange={(_, data) => setDialogOpen(data.open)}>
                <DialogSurface style={{ maxWidth: '720px' }}>
                    <DialogBody>
                        <DialogTitle>{isEditing ? 'Editar programación' : 'Nueva programación'}</DialogTitle>
                        <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: '18px', marginTop: '12px' }}>
                            <div className={styles.dialogGrid}>
                                <div className={`${styles.filterField} ${styles.fullWidth}`}>
                                    <Label required>Empresa</Label>
                                    {companies.length === 1 ? <Input value={companies[0].displayName} readOnly /> : (
                                        <Select value={String(formCompanyId)} onChange={(_, data) => setFormCompanyId(Number(data.value))}>
                                            <option value="0">Seleccionar empresa...</option>
                                            {companies.map(company => <option key={company.id} value={company.id}>{company.displayName}</option>)}
                                        </Select>
                                    )}
                                </div>
                                <div className={styles.filterField}>
                                    <Label required>Hora</Label>
                                    <Select value={String(formHour)} onChange={(_, data) => setFormHour(Number(data.value))}>
                                        {Array.from({ length: 24 }, (_, hour) => <option key={hour} value={hour}>{hour.toString().padStart(2, '0')}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterField}>
                                    <Label required>Minuto</Label>
                                    <Select value={String(formMinute)} onChange={(_, data) => setFormMinute(Number(data.value))}>
                                        {[0, 15, 30, 45].map(minute => <option key={minute} value={minute}>{minute.toString().padStart(2, '0')}</option>)}
                                    </Select>
                                </div>
                                <div className={`${styles.filterField} ${styles.fullWidth}`}>
                                    <Label required>Días de la semana</Label>
                                    <div className={styles.daySelector}>
                                        {ALL_DAYS.map(day => (
                                            <button key={day.key} type="button" className={`${styles.dayButton} ${formDays.includes(day.key) ? styles.dayButtonActive : ''}`} onClick={() => setFormDays(prev => prev.includes(day.key) ? prev.filter(item => item !== day.key) : [...prev, day.key])}>
                                                {day.label}
                                            </button>
                                        ))}
                                    </div>
                                </div>
                                <div className={`${styles.filterField} ${styles.fullWidth}`}>
                                    <Switch checked={formEnabled} onChange={(_, data) => setFormEnabled(data.checked)} label={formEnabled ? 'Programación habilitada' : 'Programación deshabilitada'} />
                                </div>
                                {formDays.length > 0 && (
                                    <div className={`${styles.previewBox} ${styles.fullWidth}`}>
                                        <Text weight="semibold">Vista previa</Text>
                                        <Text>Se ejecutará los {formatDays(formDays.join(','))} a las {formatTime(formHour, formMinute)}.</Text>
                                    </div>
                                )}
                            </div>
                        </DialogContent>
                        <DialogActions>
                            <Button appearance="secondary" onClick={() => setDialogOpen(false)} disabled={saving}>Cancelar</Button>
                            <Button appearance="primary" onClick={handleSave} disabled={saving || formDays.length === 0 || formCompanyId === 0} icon={saving ? <Spinner size="tiny" /> : <ClockRegular />}>
                                {saving ? 'Guardando...' : isEditing ? 'Guardar cambios' : 'Crear programación'}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </div>
    );
};

export default SchedulesPage;
