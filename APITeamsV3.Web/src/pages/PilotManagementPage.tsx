import React, { useEffect, useState, useCallback } from 'react';
import {
    Button,
    Spinner,
    Badge,
    Checkbox,
    Tooltip,
    Input,
    Select,
    Switch,
} from '@fluentui/react-components';
import {
    BeakerRegular,
    DeleteRegular,
    AddCircleRegular,
    ArrowSyncRegular,
    CheckmarkCircleRegular,
    DismissCircleRegular,
    SearchRegular,
    CopyRegular,
    CheckmarkRegular,
    BuildingRegular,
    FilterRegular,
    CodeRegular,
    PlayCircleRegular,
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';
import { showError, showConfirm } from '../utils/alerts';
import type { CompanyConfig } from '../types/CompanyConfig';

interface CandidateSection {
    idSeccion: number;
    codigo: string;
    tipoServicio?: string;
    fechaInicio?: string;
    fechaFin?: string;
    periodoInicio?: string;
    periodoFin?: string;
    esTeams?: boolean;
}

interface CurrentPilotSection {
    id: number;
    companyConfigId: number;
    idSeccion: number;
}

// Periodos por empresa (companyKey)
const PILOT_PERIODS: Record<string, string> = {
    zegel: '2026-IIIA',
    idat: '2026-IIIA',
};

const DEFAULT_PERIOD = '2026-IIIA';

const PilotManagementPage: React.FC = () => {
    const api = useApiClient();

    // Estado general
    const [companies, setCompanies] = useState<CompanyConfig[]>([]);
    const [selectedCompany, setSelectedCompany] = useState<CompanyConfig | null>(null);
    const [loadingCompanies, setLoadingCompanies] = useState(true);

    // Candidatos y filtros
    const [candidates, setCandidates] = useState<CandidateSection[]>([]);
    const [loadingCandidates, setLoadingCandidates] = useState(false);
    const [candidateFilter, setCandidateFilter] = useState('');
    const [selectedCandidates, setSelectedCandidates] = useState<Set<number>>(new Set());
    const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'upcoming' | 'expired'>('all');
    const [onlyEsTeams, setOnlyEsTeams] = useState(false);

    // Actuales
    const [currentSections, setCurrentSections] = useState<CurrentPilotSection[]>([]);
    const [loadingCurrent, setLoadingCurrent] = useState(false);

    // Acciones
    const [adding, setAdding] = useState(false);
    const [periodoCodigo, setPeriodoCodigo] = useState(DEFAULT_PERIOD);
    const [availablePeriods, setAvailablePeriods] = useState<string[]>([]);
    const [loadingPeriods, setLoadingPeriods] = useState(false);

    // Feedback de copiar & inspector de reglas
    const [copiedSp, setCopiedSp] = useState(false);
    const [showRuleInspector, setShowRuleInspector] = useState(true);

    // ─── Cargar empresas ───────────────────────────────────────────────────
    useEffect(() => {
        const loadCompanies = async () => {
            try {
                setLoadingCompanies(true);
                const res = await api.get('/admin/company-configs');
                const data: CompanyConfig[] = res.data;
                setCompanies(data.filter(c => c.isActive));
            } catch (err) {
                showError('No se pudieron cargar las empresas.');
            } finally {
                setLoadingCompanies(false);
            }
        };
        loadCompanies();
    }, []);

    const loadAvailablePeriods = async (companyId: number) => {
        try {
            setLoadingPeriods(true);
            const res = await api.get(`/admin/pilot-management/${companyId}/periods`);
            const periodsList: string[] = (res.data || []).map((p: { codigo: string }) => p.codigo);
            setAvailablePeriods(periodsList);
        } catch (err) {
            console.error('Error al cargar periodos:', err);
            setAvailablePeriods([]);
        } finally {
            setLoadingPeriods(false);
        }
    };

    // ─── Al seleccionar empresa, inferir el periodo y cargar datos ─────────
    const handleSelectCompany = useCallback(async (company: CompanyConfig) => {
        setSelectedCompany(company);
        setSelectedCandidates(new Set());
        setCandidates([]);
        setCandidateFilter('');

        // Inferir periodo según companyKey
        const periodo = PILOT_PERIODS[company.companyKey?.toLowerCase()] ?? DEFAULT_PERIOD;
        setPeriodoCodigo(periodo);

        loadAvailablePeriods(company.id);
        await Promise.all([
            loadCandidates(company.id, periodo, onlyEsTeams),
            loadCurrentSections(company.id),
        ]);
    }, [onlyEsTeams]);

    const loadCandidates = async (companyId: number, periodo: string, filterEsTeams: boolean = onlyEsTeams) => {
        try {
            setLoadingCandidates(true);
            const url = `/admin/pilot-management/${companyId}/candidates?periodoCodigo=${encodeURIComponent(periodo)}&onlyEsTeams=${filterEsTeams}`;
            const res = await api.get(url);
            setCandidates(res.data);
        } catch (err: any) {
            const msg = err?.response?.data?.message || err?.message || 'Error al cargar candidatos.';
            showError(msg);
            setCandidates([]);
        } finally {
            setLoadingCandidates(false);
        }
    };

    const loadCurrentSections = async (companyId: number) => {
        try {
            setLoadingCurrent(true);
            const res = await api.get(`/admin/pilot-management/${companyId}/sections`);
            setCurrentSections(res.data);
        } catch (err) {
            showError('Error al cargar secciones del piloto.');
            setCurrentSections([]);
        } finally {
            setLoadingCurrent(false);
        }
    };

    // ─── Reload candidatos con el periodo actual ───────────────────────────
    const handleReloadCandidates = () => {
        if (!selectedCompany) return;
        setSelectedCandidates(new Set());
        loadCandidates(selectedCompany.id, periodoCodigo, onlyEsTeams);
    };

    // ─── Toggle selección de candidato ────────────────────────────────────
    const toggleCandidate = (idSeccion: number) => {
        setSelectedCandidates(prev => {
            const next = new Set(prev);
            if (next.has(idSeccion)) next.delete(idSeccion);
            else next.add(idSeccion);
            return next;
        });
    };

    const toggleAll = (filtered: CandidateSection[]) => {
        const allIds = new Set(filtered.map(c => c.idSeccion));
        const allSelected = filtered.every(c => selectedCandidates.has(c.idSeccion));
        if (allSelected) {
            setSelectedCandidates(prev => {
                const next = new Set(prev);
                allIds.forEach(id => next.delete(id));
                return next;
            });
        } else {
            setSelectedCandidates(prev => {
                const next = new Set(prev);
                allIds.forEach(id => next.add(id));
                return next;
            });
        }
    };

    // ─── Agregar seleccionados al piloto ──────────────────────────────────
    const handleBulkAdd = async () => {
        if (!selectedCompany || selectedCandidates.size === 0) return;

        const alreadyInPilot = new Set(currentSections.map(s => s.idSeccion));
        const toAdd = [...selectedCandidates].filter(id => !alreadyInPilot.has(id));

        if (toAdd.length === 0) {
            showError('Todas las secciones seleccionadas ya están en el piloto.');
            return;
        }

        setAdding(true);
        try {
            const res = await api.post(`/admin/pilot-management/${selectedCompany.id}/sections/bulk`, {
                idSecciones: toAdd,
                periodo: periodoCodigo
            });
            const added: number = res.data?.added ?? 0;
            await loadCurrentSections(selectedCompany.id);
            setSelectedCandidates(new Set());

            if (added > 0) {
                const { default: Swal } = await import('sweetalert2');
                Swal.fire({
                    icon: 'success',
                    title: `${added} sección${added !== 1 ? 'es' : ''} agregada${added !== 1 ? 's' : ''} al piloto`,
                    timer: 2500,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end',
                });
            }
        } catch (err: any) {
            showError(err?.response?.data?.message || 'Error al agregar secciones.');
        } finally {
            setAdding(false);
        }
    };

    // ─── Quitar sección del piloto ────────────────────────────────────────
    const handleRemove = async (section: CurrentPilotSection) => {
        const result = await showConfirm(`¿Quitar IdSeccion ${section.idSeccion} del piloto?`);
        if (!result.isConfirmed) return;

        try {
            await api.delete(`/admin/pilot-management/${section.companyConfigId}/sections/${section.id}`);
            setCurrentSections(prev => prev.filter(s => s.id !== section.id));
        } catch (err) {
            showError('Error al eliminar la sección del piloto.');
        }
    };

    // Copiar IDs al portapapeles
    const handleCopySpIds = () => {
        const text = currentSections.map(s => `(${s.idSeccion})`).join(',\n');
        navigator.clipboard.writeText(text);
        setCopiedSp(true);
        setTimeout(() => setCopiedSp(false), 2200);
    };

    // Helper: Evaluar Estado de Ventana de Sincronización y Fecha de Inicio de Creación de Equipos
    const getDateWindowStatus = (cand: CandidateSection) => {
        const isCE = cand.tipoServicio === 'C';
        const startStr = isCE ? cand.periodoInicio : cand.fechaInicio;
        const endStr = isCE ? cand.periodoFin : cand.fechaFin;

        if (!startStr || !endStr) return {
            status: 'unknown',
            label: 'Fechas N/A',
            color: '#64748b',
            bgColor: '#f1f5f9',
            borderColor: '#cbd5e1',
            creationStartDate: 'No definida',
            classStartDate: 'No definida',
            range: 'Fechas no definidas'
        };

        const startDate = new Date(startStr);
        const endDate = new Date(endStr);
        const now = new Date();

        // Rango de creación de equipos: Inicio - 14 días (margen del SP / Parametro EsTeams)
        const creationStartDate = new Date(startDate);
        creationStartDate.setDate(creationStartDate.getDate() - 14);

        const activeEnd = new Date(endDate);
        activeEnd.setDate(activeEnd.getDate() + 14);

        const formatDate = (d: Date) => d.toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' });
        const formatDateShort = (d: Date) => d.toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit' });

        const creationStartDateStr = formatDate(creationStartDate);
        const classStartDateStr = formatDate(startDate);
        const syncEndDateStr = formatDate(endDate);

        if (now >= creationStartDate && now <= activeEnd) {
            return {
                status: 'active',
                label: 'En Ventana (Gestionando)',
                color: '#15803d',
                bgColor: '#f0fdf4',
                borderColor: '#bbf7d0',
                creationStartDate: creationStartDateStr,
                classStartDate: classStartDateStr,
                syncEndDate: syncEndDateStr,
                range: `Creación activa desde ${formatDateShort(creationStartDate)} (Fin: ${formatDateShort(endDate)})`
            };
        } else if (now < creationStartDate) {
            return {
                status: 'upcoming',
                label: 'Próxima Gestión',
                color: '#b45309',
                bgColor: '#fffbeb',
                borderColor: '#fde68a',
                creationStartDate: creationStartDateStr,
                classStartDate: classStartDateStr,
                syncEndDate: syncEndDateStr,
                range: `Inicia creación el ${creationStartDateStr}`
            };
        } else {
            return {
                status: 'expired',
                label: 'Gestión Finalizada',
                color: '#475569',
                bgColor: '#f8fafc',
                borderColor: '#e2e8f0',
                creationStartDate: creationStartDateStr,
                classStartDate: classStartDateStr,
                syncEndDate: syncEndDateStr,
                range: `Concluyó el ${syncEndDateStr}`
            };
        }
    };

    // ─── Candidatos filtrados ─────────────────────────────────────────────
    const filteredCandidates = candidates.filter(c => {
        const matchesText = c.codigo.toLowerCase().includes(candidateFilter.toLowerCase()) || String(c.idSeccion).includes(candidateFilter);
        if (!matchesText) return false;

        if (statusFilter === 'all') return true;
        const win = getDateWindowStatus(c);
        return win.status === statusFilter;
    });

    // IDs ya en piloto para marcarlos
    const pilotIds = new Set(currentSections.map(s => s.idSeccion));

    // Candidatos no en piloto (nuevos)
    const newCandidates = filteredCandidates.filter(c => !pilotIds.has(c.idSeccion));
    const alreadyInPilot = filteredCandidates.filter(c => pilotIds.has(c.idSeccion));

    const allNewSelected = newCandidates.length > 0 && newCandidates.every(c => selectedCandidates.has(c.idSeccion));

    // Ejemplo de fecha de creación del primer candidato para el banner de regla
    const sampleCandidate = candidates.find(c => c.fechaInicio || c.periodoInicio);
    const sampleDates = sampleCandidate ? getDateWindowStatus(sampleCandidate) : null;

    // ─── Render ───────────────────────────────────────────────────────────
    return (
        <div style={{ padding: '36px 40px', maxWidth: '1440px', margin: '0 auto', fontFamily: 'system-ui, -apple-system, sans-serif' }}>
            {/* Header principal */}
            <div style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '32px',
                paddingBottom: '20px',
                borderBottom: '1px solid #e2e8f0'
            }}>
                <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '8px' }}>
                        <div style={{
                            width: '44px',
                            height: '44px',
                            borderRadius: '12px',
                            background: 'linear-gradient(135deg, #7c3aed 0%, #4f46e5 100%)',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            boxShadow: '0 8px 16px -4px rgba(124, 58, 237, 0.3)'
                        }}>
                            <BeakerRegular style={{ fontSize: '24px', color: 'white' }} />
                        </div>
                        <div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                <h1 style={{ margin: 0, fontSize: '26px', fontWeight: 800, color: '#0f172a', letterSpacing: '-0.02em' }}>
                                    Gestión de Inicios de Piloto
                                </h1>
                                <Badge appearance="tint" color="brand" style={{ borderRadius: '6px', fontWeight: 700 }}>V3.0</Badge>
                            </div>
                            <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: '14px' }}>
                                Configura qué secciones participan en el piloto API Teams V3 y quedan excluidas del SP legacy v1 (<code>cTeamsPorSeccion</code>).
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            {/* Selector de empresa */}
            <div style={{ marginBottom: '32px' }}>
                <div style={{
                    fontSize: '11px',
                    fontWeight: 800,
                    color: '#94a3b8',
                    textTransform: 'uppercase',
                    letterSpacing: '0.08em',
                    marginBottom: '14px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px'
                }}>
                    <BuildingRegular style={{ fontSize: '14px' }} /> Seleccionar Empresa / Tenant
                </div>
                {loadingCompanies ? (
                    <Spinner label="Cargando empresas registradas..." />
                ) : (
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: '16px' }}>
                        {companies.map(c => {
                            const isSelected = selectedCompany?.id === c.id;
                            const pilotPeriodo = PILOT_PERIODS[c.companyKey?.toLowerCase()] ?? DEFAULT_PERIOD;
                            const isZegel = c.companyKey?.toLowerCase().includes('zegel');
                            const initial = isZegel ? 'Z' : 'I';
                            const gradient = isZegel
                                ? 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)'
                                : 'linear-gradient(135deg, #0ea5e9 0%, #2563eb 100%)';

                            return (
                                <div
                                    key={c.id}
                                    id={`company-btn-${c.companyKey}`}
                                    onClick={() => handleSelectCompany(c)}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '16px',
                                        padding: '18px 22px',
                                        borderRadius: '16px',
                                        border: isSelected ? '2px solid #7c3aed' : '1px solid #e2e8f0',
                                        background: isSelected
                                            ? 'linear-gradient(135deg, #ffffff 0%, #faf5ff 100%)'
                                            : 'white',
                                        cursor: 'pointer',
                                        transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
                                        boxShadow: isSelected
                                            ? '0 12px 24px -6px rgba(124, 58, 237, 0.18)'
                                            : '0 2px 6px rgba(0, 0, 0, 0.03)',
                                        transform: isSelected ? 'translateY(-2px)' : 'none',
                                    }}
                                >
                                    <div style={{
                                        width: '42px',
                                        height: '42px',
                                        borderRadius: '12px',
                                        background: gradient,
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        color: 'white',
                                        fontWeight: 800,
                                        fontSize: '18px',
                                        flexShrink: 0,
                                        boxShadow: '0 4px 10px rgba(0,0,0,0.1)'
                                    }}>
                                        {initial}
                                    </div>
                                    <div style={{ flex: 1, minWidth: 0 }}>
                                        <div style={{ fontWeight: 700, fontSize: '17px', color: isSelected ? '#6d28d9' : '#1e293b', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                            {c.displayName}
                                        </div>
                                        <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                                            <span>Nuevo inicio:</span>
                                            <span style={{
                                                background: isSelected ? '#edd8ff' : '#f1f5f9',
                                                color: isSelected ? '#6d28d9' : '#475569',
                                                padding: '1px 7px',
                                                borderRadius: '6px',
                                                fontWeight: 700,
                                                fontSize: '11px'
                                            }}>
                                                {pilotPeriodo}
                                            </span>
                                        </div>
                                    </div>
                                    <Badge
                                        appearance="filled"
                                        color={c.isPilotMode ? 'success' : 'informative'}
                                        style={{ fontSize: '10px', borderRadius: '12px', padding: '2px 8px' }}
                                    >
                                        {c.isPilotMode ? 'Piloto ON' : 'Piloto OFF'}
                                    </Badge>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Contenido principal */}
            {selectedCompany && (
                <>
                    {/* Banner superior de empresa */}
                    <div style={{
                        background: 'linear-gradient(135deg, #4f46e5 0%, #7c3aed 50%, #9333ea 100%)',
                        borderRadius: '18px',
                        padding: '22px 28px',
                        marginBottom: '24px',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        color: 'white',
                        boxShadow: '0 16px 32px -8px rgba(124, 58, 237, 0.35)',
                        position: 'relative',
                        overflow: 'hidden',
                    }}>
                        <div style={{ position: 'relative', zIndex: 1 }}>
                            <div style={{ fontWeight: 800, fontSize: '22px', letterSpacing: '-0.01em' }}>
                                {selectedCompany.displayName}
                            </div>
                            <div style={{ fontSize: '13px', opacity: 0.9, marginTop: '4px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                                <span>Período nuevo inicio: <strong style={{ textDecoration: 'underline' }}>{periodoCodigo}</strong></span>
                                <span>•</span>
                                <span>Secciones en piloto: <strong>{currentSections.length}</strong></span>
                            </div>
                        </div>

                        <div style={{ display: 'flex', gap: '14px', alignItems: 'center', position: 'relative', zIndex: 1 }}>
                            {/* Switch PE.EsTeams = 1 */}
                            <div style={{
                                background: 'rgba(255,255,255,0.18)',
                                backdropFilter: 'blur(8px)',
                                padding: '4px 12px',
                                borderRadius: '10px',
                                border: '1px solid rgba(255,255,255,0.3)',
                                display: 'flex',
                                alignItems: 'center',
                            }}>
                                <Switch
                                    id="es-teams-header-switch"
                                    checked={onlyEsTeams}
                                    onChange={(_, d) => {
                                        setOnlyEsTeams(d.checked);
                                        if (selectedCompany) loadCandidates(selectedCompany.id, periodoCodigo, d.checked);
                                    }}
                                    label={
                                        <span style={{ fontSize: '12px', fontWeight: 700, color: 'white', whiteSpace: 'nowrap' }}>
                                            PE.EsTeams = 1
                                        </span>
                                    }
                                />
                            </div>

                            <div style={{ fontSize: '12px', fontWeight: 600, opacity: 0.9, display: 'flex', alignItems: 'center', gap: '4px' }}>
                                <FilterRegular /> Período:
                            </div>
                            <Select
                                id="periodo-select"
                                value={periodoCodigo}
                                onChange={(_, d) => {
                                    setPeriodoCodigo(d.value);
                                    if (selectedCompany) loadCandidates(selectedCompany.id, d.value, onlyEsTeams);
                                }}
                                style={{ background: 'white', color: '#1e293b', minWidth: '140px', fontSize: '13px', borderRadius: '8px' }}
                                size="small"
                                disabled={loadingPeriods}
                            >
                                {availablePeriods.length > 0 ? (
                                    Array.from(new Set([periodoCodigo, ...availablePeriods])).map(p => (
                                        <option key={p} value={p}>{p}</option>
                                    ))
                                ) : (
                                    <option value={periodoCodigo}>{periodoCodigo}</option>
                                )}
                            </Select>

                            <Input
                                id="periodo-input"
                                value={periodoCodigo}
                                onChange={(_, d) => setPeriodoCodigo(d.value)}
                                placeholder="Otro..."
                                style={{ background: 'white', color: '#1e293b', width: '90px', fontSize: '13px', borderRadius: '8px' }}
                                size="small"
                            />

                            <Button
                                id="reload-candidates-btn"
                                icon={<ArrowSyncRegular />}
                                appearance="secondary"
                                size="small"
                                onClick={handleReloadCandidates}
                                disabled={loadingCandidates}
                                style={{
                                    background: 'rgba(255,255,255,0.2)',
                                    color: 'white',
                                    border: '1px solid rgba(255,255,255,0.4)',
                                    borderRadius: '8px',
                                    fontWeight: 600,
                                    backdropFilter: 'blur(8px)'
                                }}
                            >
                                Buscar
                            </Button>
                        </div>
                    </div>

                    {/* Inspector Visual de la Regla de Consulta SQL y Fecha de Inicio de Gestión */}
                    <div style={{
                        background: 'white',
                        borderRadius: '16px',
                        padding: '18px 24px',
                        marginBottom: '24px',
                        border: '1px solid #e2e8f0',
                        boxShadow: '0 4px 16px -2px rgba(0,0,0,0.04)',
                    }}>
                        <div
                            style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', cursor: 'pointer' }}
                            onClick={() => setShowRuleInspector(prev => !prev)}
                        >
                            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                                <div style={{ background: '#eff6ff', color: '#2563eb', padding: '8px', borderRadius: '10px', display: 'flex' }}>
                                    <CodeRegular style={{ fontSize: '20px' }} />
                                </div>
                                <div>
                                    <div style={{ fontWeight: 700, fontSize: '15px', color: '#0f172a' }}>
                                        Regla de Elegibilidad y Fecha de Creación de Equipos
                                    </div>
                                    <div style={{ fontSize: '12px', color: '#64748b' }}>
                                        Fechas de activación y creación automática ejecutadas por <code>cTeamsPorSeccion</code> y <code>SectionEligibilityService</code>
                                    </div>
                                </div>
                            </div>
                            <Button appearance="subtle" size="small">
                                {showRuleInspector ? 'Ocultar Regla' : 'Ver Regla & Fechas'}
                            </Button>
                        </div>

                        {showRuleInspector && (
                            <div style={{ marginTop: '16px', paddingTop: '16px', borderTop: '1px solid #f1f5f9' }}>
                                {/* Banner Destacado de Inicio de Creación de Equipos */}
                                {sampleDates && sampleDates.creationStartDate !== 'No definida' && (
                                    <div style={{
                                        margin: '0 0 16px 0',
                                        padding: '14px 18px',
                                        background: 'linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%)',
                                        border: '1px solid #86efac',
                                        borderRadius: '12px',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'space-between',
                                        color: '#14532d',
                                    }}>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                                            <PlayCircleRegular style={{ fontSize: '24px', color: '#16a34a' }} />
                                            <div>
                                                <div style={{ fontWeight: 800, fontSize: '14px' }}>
                                                    🚀 Fecha de Inicio de Creación de Equipos y Gestión: <span style={{ textDecoration: 'underline' }}>{sampleDates.creationStartDate}</span>
                                                </div>
                                                <div style={{ fontSize: '12px', color: '#166534', marginTop: '2px' }}>
                                                    La aplicación inicia la provisión automática <strong>14 días antes</strong> del inicio oficial de clases (Inicio de clases: {sampleDates.classStartDate}).
                                                </div>
                                            </div>
                                        </div>
                                        <Badge appearance="filled" color="success" style={{ borderRadius: '10px', fontSize: '11px', padding: '3px 10px' }}>
                                            Ventana -14 días
                                        </Badge>
                                    </div>
                                )}

                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '16px' }}>
                                    <div style={{
                                        background: onlyEsTeams ? '#f0fdf4' : '#f8fafc',
                                        padding: '14px',
                                        borderRadius: '12px',
                                        border: onlyEsTeams ? '1px solid #bbf7d0' : '1px solid #e2e8f0',
                                        transition: 'all 0.2s ease'
                                    }}>
                                        <div style={{ fontWeight: 700, fontSize: '13px', color: '#1e293b', marginBottom: '6px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                                            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                                <Badge appearance="filled" color={onlyEsTeams ? 'success' : 'brand'} style={{ borderRadius: '6px' }}>1</Badge>
                                                Flag Teams en Periodo
                                            </span>
                                            <Switch
                                                id="es-teams-inspector-switch"
                                                checked={onlyEsTeams}
                                                onChange={(_, d) => {
                                                    setOnlyEsTeams(d.checked);
                                                    if (selectedCompany) loadCandidates(selectedCompany.id, periodoCodigo, d.checked);
                                                }}
                                            />
                                        </div>
                                        <code style={{ fontSize: '12px', color: onlyEsTeams ? '#15803d' : '#4338ca', background: onlyEsTeams ? '#dcfce7' : '#e0e7ff', padding: '2px 6px', borderRadius: '4px' }}>
                                            {onlyEsTeams ? 'PE.EsTeams = 1 (ACTIVO)' : 'PE.EsTeams (AMBOS)'}
                                        </code>
                                        <div style={{ fontSize: '11px', color: '#64748b', marginTop: '6px' }}>
                                            {onlyEsTeams
                                                ? 'Filtrando únicamente secciones donde el Periodo tiene Teams habilitado.'
                                                : 'Mostrando todas las secciones del periodo (Switch apagado).'}
                                        </div>
                                    </div>

                                    <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '12px', border: '1px solid #e2e8f0' }}>
                                        <div style={{ fontWeight: 700, fontSize: '13px', color: '#1e293b', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                                            <Badge appearance="filled" color="important" style={{ borderRadius: '6px' }}>2</Badge> Servicios Presencial/Licenciatura (P / L)
                                        </div>
                                        <code style={{ fontSize: '11px', color: '#0369a1', background: '#e0f2fe', padding: '2px 6px', borderRadius: '4px' }}>
                                            GETDATE() BETWEEN (SE.FechaInicio - 14d) AND (SE.FechaFin + 14d)
                                        </code>
                                        <div style={{ fontSize: '11px', color: '#64748b', marginTop: '6px' }}>
                                            Rango de fechas de sincronización calculado por la <strong>Sección</strong>.
                                        </div>
                                    </div>

                                    <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '12px', border: '1px solid #e2e8f0' }}>
                                        <div style={{ fontWeight: 700, fontSize: '13px', color: '#1e293b', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                                            <Badge appearance="filled" color="informative" style={{ borderRadius: '6px' }}>3</Badge> Educación Continua (C)
                                        </div>
                                        <code style={{ fontSize: '11px', color: '#15803d', background: '#dcfce7', padding: '2px 6px', borderRadius: '4px' }}>
                                            GETDATE() BETWEEN (PE.Inicio - 14d) AND (PE.Fin + 14d)
                                        </code>
                                        <div style={{ fontSize: '11px', color: '#64748b', marginTop: '6px' }}>
                                            Rango de fechas de sincronización calculado por el <strong>Periodo</strong>.
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Paneles principales */}
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '24px' }}>

                        {/* Panel izquierdo: Secciones Candidatas */}
                        <div style={{
                            background: 'white',
                            borderRadius: '16px',
                            boxShadow: '0 4px 20px -2px rgba(0,0,0,0.05), 0 2px 6px -1px rgba(0,0,0,0.03)',
                            border: '1px solid #e2e8f0',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                        }}>
                            {/* Cabecera panel */}
                            <div style={{
                                padding: '18px 24px',
                                borderBottom: '1px solid #f1f5f9',
                                background: 'linear-gradient(to right, #ffffff, #f8fafc)',
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                            }}>
                                <div>
                                    <div style={{ fontWeight: 700, fontSize: '16px', color: '#0f172a', display: 'flex', alignItems: 'center', gap: '8px' }}>
                                        Secciones Candidatas
                                    </div>
                                    <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
                                        Período <code style={{ background: '#e0e7ff', color: '#4338ca', padding: '1px 6px', borderRadius: '4px', fontWeight: 700 }}>{periodoCodigo}</code>
                                        {' · '}<strong style={{ color: '#0f172a' }}>{candidates.length}</strong> encontradas
                                        {onlyEsTeams && <span style={{ color: '#16a34a', fontWeight: 700, marginLeft: '6px' }}>(PE.EsTeams = 1)</span>}
                                    </div>
                                </div>
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    {selectedCandidates.size > 0 && (
                                        <Badge appearance="filled" color="brand" style={{ borderRadius: '10px' }}>{selectedCandidates.size} sel.</Badge>
                                    )}
                                    <Button
                                        id="bulk-add-btn"
                                        icon={adding ? <Spinner size="tiny" /> : <AddCircleRegular />}
                                        appearance="primary"
                                        size="small"
                                        disabled={selectedCandidates.size === 0 || adding}
                                        onClick={handleBulkAdd}
                                        style={{
                                            borderRadius: '8px',
                                            fontWeight: 600,
                                            boxShadow: selectedCandidates.size > 0 ? '0 4px 12px rgba(124, 58, 237, 0.3)' : 'none'
                                        }}
                                    >
                                        {adding ? 'Agregando...' : `Agregar (${selectedCandidates.size})`}
                                    </Button>
                                </div>
                            </div>

                            {/* Filtros de estado de ventana y buscador */}
                            <div style={{ padding: '12px 20px', borderBottom: '1px solid #f1f5f9', background: '#fafafa', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                                <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                                    <Input
                                        id="candidate-filter-input"
                                        value={candidateFilter}
                                        onChange={(_, d) => setCandidateFilter(d.value)}
                                        placeholder="Filtrar por código de sección o ID..."
                                        contentBefore={<SearchRegular style={{ color: '#94a3b8' }} />}
                                        style={{ flex: 1, background: 'white', borderRadius: '8px' }}
                                        size="small"
                                    />
                                    <Switch
                                        id="es-teams-panel-switch"
                                        checked={onlyEsTeams}
                                        onChange={(_, d) => {
                                            setOnlyEsTeams(d.checked);
                                            if (selectedCompany) loadCandidates(selectedCompany.id, periodoCodigo, d.checked);
                                        }}
                                        label={
                                            <span style={{ fontSize: '11px', fontWeight: 700, color: '#475569', whiteSpace: 'nowrap' }}>
                                                PE.EsTeams=1
                                            </span>
                                        }
                                    />
                                </div>

                                {/* Chips de filtro por estado de ventana de fecha */}
                                <div style={{ display: 'flex', gap: '6px', overflowX: 'auto', paddingBottom: '2px' }}>
                                    {[
                                        { key: 'all', label: 'Todas', count: candidates.length },
                                        { key: 'active', label: '🟢 En Ventana', count: candidates.filter(c => getDateWindowStatus(c).status === 'active').length },
                                        { key: 'upcoming', label: '🟡 Próximas', count: candidates.filter(c => getDateWindowStatus(c).status === 'upcoming').length },
                                        { key: 'expired', label: '🔴 Finalizadas', count: candidates.filter(c => getDateWindowStatus(c).status === 'expired').length },
                                    ].map(f => (
                                        <button
                                            key={f.key}
                                            onClick={() => setStatusFilter(f.key as any)}
                                            style={{
                                                padding: '4px 10px',
                                                borderRadius: '20px',
                                                fontSize: '11px',
                                                fontWeight: 600,
                                                border: statusFilter === f.key ? '1.5px solid #7c3aed' : '1px solid #cbd5e1',
                                                background: statusFilter === f.key ? '#f5f3ff' : 'white',
                                                color: statusFilter === f.key ? '#6d28d9' : '#475569',
                                                cursor: 'pointer',
                                                whiteSpace: 'nowrap',
                                                transition: 'all 0.15s ease'
                                            }}
                                        >
                                            {f.label} ({f.count})
                                        </button>
                                    ))}
                                </div>
                            </div>

                            {/* Lista scrolleable de candidatos */}
                            <div style={{ maxHeight: '490px', minHeight: '360px', overflowY: 'auto' }}>
                                {loadingCandidates ? (
                                    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '240px' }}>
                                        <Spinner label="Consultando candidatos en Smart DB..." />
                                    </div>
                                ) : filteredCandidates.length === 0 ? (
                                    <div style={{ padding: '48px 24px', textAlign: 'center', color: '#94a3b8' }}>
                                        <DismissCircleRegular style={{ fontSize: '36px', display: 'block', margin: '0 auto 12px', color: '#cbd5e1' }} />
                                        <div style={{ fontWeight: 600, color: '#64748b' }}>No se encontraron secciones</div>
                                        <div style={{ fontSize: '12px', marginTop: '4px', color: '#94a3b8' }}>
                                            {onlyEsTeams ? 'Intenta desactivar el Switch de PE.EsTeams = 1' : 'Prueba seleccionando otro período o limpiando el filtro'}
                                        </div>
                                    </div>
                                ) : (
                                    <>
                                        {/* Acciones bulk de selección */}
                                        {newCandidates.length > 0 && (
                                            <div style={{
                                                padding: '10px 20px',
                                                borderBottom: '1px solid #f1f5f9',
                                                background: '#f8fafc',
                                                display: 'flex',
                                                alignItems: 'center',
                                                gap: '8px',
                                            }}>
                                                <Checkbox
                                                    id="select-all-checkbox"
                                                    checked={allNewSelected}
                                                    onChange={() => toggleAll(newCandidates)}
                                                    label={<span style={{ fontSize: '12px', fontWeight: 600, color: '#475569' }}>Seleccionar todas las nuevas ({newCandidates.length})</span>}
                                                />
                                            </div>
                                        )}

                                        {/* Nuevas secciones candidatas con tarjeta visual enriquecida */}
                                        {newCandidates.map(c => {
                                            const isSelected = selectedCandidates.has(c.idSeccion);
                                            const win = getDateWindowStatus(c);
                                            const isCE = c.tipoServicio === 'C';

                                            return (
                                                <div key={c.idSeccion} style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '12px',
                                                    padding: '12px 20px',
                                                    borderBottom: '1px solid #f8fafc',
                                                    background: isSelected ? '#f5f3ff' : 'white',
                                                    transition: 'all 0.15s ease',
                                                    cursor: 'pointer',
                                                }}
                                                    onClick={() => toggleCandidate(c.idSeccion)}
                                                >
                                                    <Checkbox
                                                        id={`cand-${c.idSeccion}`}
                                                        checked={isSelected}
                                                        onChange={() => toggleCandidate(c.idSeccion)}
                                                        onClick={e => e.stopPropagation()}
                                                    />
                                                    <div style={{ flex: 1, minWidth: 0 }}>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                            <div style={{ fontWeight: 700, fontSize: '13px', color: isSelected ? '#6d28d9' : '#1e293b' }}>
                                                                {c.codigo}
                                                            </div>
                                                            <span style={{
                                                                fontSize: '10px',
                                                                fontWeight: 700,
                                                                padding: '1px 6px',
                                                                borderRadius: '4px',
                                                                background: isCE ? '#e0f2fe' : '#f3e8ff',
                                                                color: isCE ? '#0369a1' : '#6b21a8'
                                                            }}>
                                                                {isCE ? 'Servicio C' : 'Servicio P/L'}
                                                            </span>

                                                            {c.esTeams && (
                                                                <span style={{
                                                                    fontSize: '10px',
                                                                    fontWeight: 700,
                                                                    padding: '1px 6px',
                                                                    borderRadius: '4px',
                                                                    background: '#dcfce7',
                                                                    color: '#15803d'
                                                                }}>
                                                                    PE.EsTeams: 1
                                                                </span>
                                                            )}
                                                        </div>

                                                        {/* Información detallada de inicio de creación de equipos */}
                                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px', marginTop: '4px' }}>
                                                            <div style={{ fontSize: '11px', color: '#16a34a', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                                <PlayCircleRegular style={{ fontSize: '12px' }} />
                                                                Inicio Creación de Equipos: <strong>{win.creationStartDate}</strong>
                                                            </div>
                                                            <div style={{ fontSize: '11px', color: '#64748b', display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                                <span>ID: <strong>{c.idSeccion}</strong></span>
                                                                <span>•</span>
                                                                <span>Inicio clases: {win.classStartDate}</span>
                                                            </div>
                                                        </div>
                                                    </div>

                                                    {/* Badge de Estado de Ventana de Sincronización */}
                                                    <span style={{
                                                        fontSize: '11px',
                                                        fontWeight: 700,
                                                        padding: '3px 9px',
                                                        borderRadius: '12px',
                                                        background: win.bgColor,
                                                        color: win.color,
                                                        border: `1px solid ${win.borderColor}`,
                                                        whiteSpace: 'nowrap'
                                                    }}>
                                                        {win.label}
                                                    </span>
                                                </div>
                                            );
                                        })}

                                        {/* Secciones ya registradas en el piloto */}
                                        {alreadyInPilot.length > 0 && (
                                            <>
                                                <div style={{
                                                    padding: '8px 20px',
                                                    background: '#f0fdf4',
                                                    borderTop: '1px solid #dcfce7',
                                                    borderBottom: '1px solid #dcfce7',
                                                    fontSize: '11px',
                                                    color: '#166534',
                                                    fontWeight: 700,
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '6px'
                                                }}>
                                                    <CheckmarkCircleRegular style={{ fontSize: '14px' }} />
                                                    Secciones ya registradas en el piloto ({alreadyInPilot.length})
                                                </div>
                                                {alreadyInPilot.map(c => (
                                                    <div key={c.idSeccion} style={{
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        gap: '12px',
                                                        padding: '10px 20px',
                                                        borderBottom: '1px solid #f8fafc',
                                                        background: '#fafafa',
                                                        opacity: 0.8,
                                                    }}>
                                                        <CheckmarkCircleRegular style={{ color: '#16a34a', fontSize: '16px' }} />
                                                        <div style={{ flex: 1 }}>
                                                            <div style={{ fontWeight: 600, fontSize: '13px', color: '#334155' }}>{c.codigo}</div>
                                                            <div style={{ fontSize: '11px', color: '#94a3b8' }}>IdSeccion: {c.idSeccion}</div>
                                                        </div>
                                                        <Badge appearance="filled" color="success" style={{ fontSize: '10px', borderRadius: '8px' }}>
                                                            en piloto
                                                        </Badge>
                                                    </div>
                                                ))}
                                            </>
                                        )}
                                    </>
                                )}
                            </div>
                        </div>

                        {/* Panel derecho: Secciones actualmente en el Piloto */}
                        <div style={{
                            background: 'white',
                            borderRadius: '16px',
                            boxShadow: '0 4px 20px -2px rgba(0,0,0,0.05), 0 2px 6px -1px rgba(0,0,0,0.03)',
                            border: '1px solid #e2e8f0',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                        }}>
                            {/* Cabecera panel */}
                            <div style={{
                                padding: '18px 24px',
                                borderBottom: '1px solid #f1f5f9',
                                background: 'linear-gradient(to right, #ffffff, #f8fafc)',
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                            }}>
                                <div>
                                    <div style={{ fontWeight: 700, fontSize: '16px', color: '#0f172a', display: 'flex', alignItems: 'center', gap: '8px' }}>
                                        Secciones en el Piloto
                                    </div>
                                    <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
                                        Excluidas automáticamente en SP v1 (<code>cTeamsPorSeccion</code>)
                                    </div>
                                </div>
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    <Badge appearance="filled" color="informative" style={{ borderRadius: '10px', padding: '2px 8px' }}>
                                        {currentSections.length} total
                                    </Badge>
                                    <Tooltip content="Recargar lista del piloto" relationship="label">
                                        <Button
                                            id="reload-current-btn"
                                            icon={<ArrowSyncRegular />}
                                            appearance="subtle"
                                            size="small"
                                            onClick={() => selectedCompany && loadCurrentSections(selectedCompany.id)}
                                            disabled={loadingCurrent}
                                        />
                                    </Tooltip>
                                </div>
                            </div>

                            <div style={{ maxHeight: '540px', minHeight: '360px', overflowY: 'auto' }}>
                                {loadingCurrent ? (
                                    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '240px' }}>
                                        <Spinner label="Cargando secciones en piloto..." />
                                    </div>
                                ) : currentSections.length === 0 ? (
                                    <div style={{ padding: '48px 24px', textAlign: 'center', color: '#94a3b8' }}>
                                        <BeakerRegular style={{ fontSize: '36px', display: 'block', margin: '0 auto 12px', color: '#cbd5e1' }} />
                                        <div style={{ fontWeight: 600, color: '#64748b' }}>No hay secciones individuales registradas</div>
                                        <div style={{ fontSize: '12px', color: '#94a3b8', marginTop: '4px' }}>
                                            El SP excluye automáticamente las secciones del periodo completo {periodoCodigo}
                                        </div>
                                    </div>
                                ) : (
                                    <>
                                        {/* Cuadro utilitario de IDs para el SP con botón de copiar */}
                                        <div style={{
                                            margin: '14px 20px',
                                            padding: '12px 16px',
                                            background: 'linear-gradient(135deg, #f0f4ff 0%, #eef2ff 100%)',
                                            borderRadius: '12px',
                                            border: '1px solid #c7d2fe',
                                            fontSize: '12px',
                                            color: '#3730a3',
                                        }}>
                                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                                                <div style={{ fontWeight: 700, fontSize: '12px', letterSpacing: '-0.01em' }}>
                                                    IDs manuales para el SP (cTeamsPorSeccion):
                                                </div>
                                                <Button
                                                    id="copy-sp-ids-btn"
                                                    icon={copiedSp ? <CheckmarkRegular style={{ color: '#16a34a' }} /> : <CopyRegular />}
                                                    appearance="secondary"
                                                    size="small"
                                                    onClick={handleCopySpIds}
                                                    style={{
                                                        background: copiedSp ? '#dcfce7' : 'white',
                                                        color: copiedSp ? '#15803d' : '#4338ca',
                                                        border: '1px solid #c7d2fe',
                                                        borderRadius: '6px',
                                                        fontSize: '11px',
                                                        fontWeight: 600
                                                    }}
                                                >
                                                    {copiedSp ? '¡Copiado!' : 'Copiar IDs'}
                                                </Button>
                                            </div>
                                            <div style={{
                                                maxHeight: '70px',
                                                overflowY: 'auto',
                                                background: 'white',
                                                padding: '6px 10px',
                                                borderRadius: '6px',
                                                border: '1px solid #e0e7ff'
                                            }}>
                                                <code style={{ wordBreak: 'break-all', lineHeight: 1.5, fontSize: '11px', color: '#4338ca' }}>
                                                    {currentSections.map(s => `(${s.idSeccion})`).join(',\n')}
                                                </code>
                                            </div>
                                        </div>

                                        {/* Lista de secciones actuales */}
                                        {currentSections.map(s => (
                                            <div key={s.id} style={{
                                                display: 'flex',
                                                alignItems: 'center',
                                                gap: '12px',
                                                padding: '11px 20px',
                                                borderBottom: '1px solid #f8fafc',
                                                transition: 'background 0.15s ease',
                                            }}>
                                                <CheckmarkCircleRegular style={{ color: '#7c3aed', fontSize: '18px', flexShrink: 0 }} />
                                                <div style={{ flex: 1 }}>
                                                    <div style={{ fontWeight: 700, fontSize: '13px', color: '#1e293b' }}>
                                                        IdSeccion: <span style={{ color: '#7c3aed' }}>{s.idSeccion}</span>
                                                    </div>
                                                </div>
                                                <Tooltip content="Quitar del piloto de esta empresa" relationship="label">
                                                    <Button
                                                        id={`remove-${s.id}`}
                                                        icon={<DeleteRegular />}
                                                        appearance="subtle"
                                                        size="small"
                                                        onClick={() => handleRemove(s)}
                                                        style={{ color: '#ef4444', borderRadius: '6px' }}
                                                    />
                                                </Tooltip>
                                            </div>
                                        ))}
                                    </>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Banner explicativo / Info Box */}
                    <div style={{
                        marginTop: '28px',
                        padding: '16px 20px',
                        background: 'linear-gradient(135deg, #fffbeb 0%, #fef3c7 100%)',
                        border: '1px solid #fde68a',
                        borderRadius: '14px',
                        fontSize: '13px',
                        color: '#92400e',
                        display: 'flex',
                        gap: '14px',
                        alignItems: 'center',
                        boxShadow: '0 4px 12px -2px rgba(245, 158, 11, 0.12)'
                    }}>
                        <span style={{ fontSize: '22px' }}>💡</span>
                        <div style={{ lineHeight: 1.5 }}>
                            <strong>Funcionamiento del SP v1 (<code>cTeamsPorSeccion</code>):</strong> El SP excluye automáticamente a <strong>todas las secciones del periodo completo</strong> (actualmente configurado con <code>{periodoCodigo}</code>). Las secciones que agregues individualmente aquí sirven para registrar excepciones puntuales.
                        </div>
                    </div>
                </>
            )}
        </div>
    );
};

export default PilotManagementPage;
