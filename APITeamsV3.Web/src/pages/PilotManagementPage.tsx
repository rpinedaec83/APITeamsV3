import React, { useEffect, useState, useCallback } from 'react';
import {
    Button,
    Spinner,
    Badge,
    Checkbox,
    Tooltip,
    Input,
} from '@fluentui/react-components';
import {
    BeakerRegular,
    DeleteRegular,
    AddCircleRegular,
    ArrowSyncRegular,
    CheckmarkCircleRegular,
    DismissCircleRegular,
    SearchRegular,
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';
import { showError, showConfirm } from '../utils/alerts';
import type { CompanyConfig } from '../types/CompanyConfig';

interface CandidateSection {
    idSeccion: number;
    codigo: string;
}

interface CurrentPilotSection {
    id: number;
    companyConfigId: number;
    idSeccion: number;
}

// Periodos por empresa (companyKey)
const PILOT_PERIODS: Record<string, string> = {
    zegel: '2026-IIE',
    idat: '2026-II',
};

const DEFAULT_PERIOD = '2026-IIE';

const PilotManagementPage: React.FC = () => {
    const api = useApiClient();

    // Estado general
    const [companies, setCompanies] = useState<CompanyConfig[]>([]);
    const [selectedCompany, setSelectedCompany] = useState<CompanyConfig | null>(null);
    const [loadingCompanies, setLoadingCompanies] = useState(true);

    // Candidatos
    const [candidates, setCandidates] = useState<CandidateSection[]>([]);
    const [loadingCandidates, setLoadingCandidates] = useState(false);
    const [candidateFilter, setCandidateFilter] = useState('');
    const [selectedCandidates, setSelectedCandidates] = useState<Set<number>>(new Set());

    // Actuales
    const [currentSections, setCurrentSections] = useState<CurrentPilotSection[]>([]);
    const [loadingCurrent, setLoadingCurrent] = useState(false);

    // Acciones
    const [adding, setAdding] = useState(false);
    const [periodoCodigo, setPeriodoCodigo] = useState(DEFAULT_PERIOD);

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

    // ─── Al seleccionar empresa, inferir el periodo y cargar datos ─────────
    const handleSelectCompany = useCallback(async (company: CompanyConfig) => {
        setSelectedCompany(company);
        setSelectedCandidates(new Set());
        setCandidates([]);
        setCandidateFilter('');

        // Inferir periodo según companyKey
        const periodo = PILOT_PERIODS[company.companyKey?.toLowerCase()] ?? DEFAULT_PERIOD;
        setPeriodoCodigo(periodo);

        await Promise.all([
            loadCandidates(company.id, periodo),
            loadCurrentSections(company.id),
        ]);
    }, []);

    const loadCandidates = async (companyId: number, periodo: string) => {
        try {
            setLoadingCandidates(true);
            const res = await api.get(`/admin/pilot-management/${companyId}/candidates?periodoCodigo=${encodeURIComponent(periodo)}`);
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
        loadCandidates(selectedCompany.id, periodoCodigo);
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
            });
            const added: number = res.data?.added ?? 0;
            await loadCurrentSections(selectedCompany.id);
            setSelectedCandidates(new Set());
            // Breve feedback visual
            if (added > 0) {
                // Toast a través del alert nativo de SweetAlert2
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

    // ─── Candidatos filtrados ─────────────────────────────────────────────
    const filteredCandidates = candidates.filter(c =>
        c.codigo.toLowerCase().includes(candidateFilter.toLowerCase()) ||
        String(c.idSeccion).includes(candidateFilter)
    );

    // IDs ya en piloto para marcarlos
    const pilotIds = new Set(currentSections.map(s => s.idSeccion));

    // Candidatos no en piloto (nuevos)
    const newCandidates = filteredCandidates.filter(c => !pilotIds.has(c.idSeccion));
    const alreadyInPilot = filteredCandidates.filter(c => pilotIds.has(c.idSeccion));

    const allNewSelected = newCandidates.length > 0 && newCandidates.every(c => selectedCandidates.has(c.idSeccion));

    // ─── Render ───────────────────────────────────────────────────────────
    return (
        <div style={{ padding: '32px', maxWidth: '1400px', margin: '0 auto' }}>
            {/* Header */}
            <div style={{ marginBottom: '28px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '6px' }}>
                    <BeakerRegular style={{ fontSize: '28px', color: '#7c3aed' }} />
                    <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 700, color: '#1a1a2e' }}>
                        Gestión de Inicios de Piloto
                    </h1>
                </div>
                <p style={{ margin: 0, color: '#666', fontSize: '14px', maxWidth: '680px' }}>
                    Administra qué secciones participan en el piloto API Teams V3 por empresa.
                    Las secciones del piloto son <strong>excluidas del SP v1</strong> (cTeamsPorSeccion).
                </p>
            </div>

            {/* Selector de empresa */}
            <div style={{ marginBottom: '28px' }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#888', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '10px' }}>
                    Seleccionar Empresa
                </div>
                {loadingCompanies ? (
                    <Spinner label="Cargando empresas..." />
                ) : (
                    <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                        {companies.map(c => {
                            const isSelected = selectedCompany?.id === c.id;
                            const pilotPeriodo = PILOT_PERIODS[c.companyKey?.toLowerCase()] ?? '—';
                            return (
                                <button
                                    key={c.id}
                                    id={`company-btn-${c.companyKey}`}
                                    onClick={() => handleSelectCompany(c)}
                                    style={{
                                        display: 'flex',
                                        flexDirection: 'column',
                                        alignItems: 'flex-start',
                                        gap: '4px',
                                        padding: '14px 20px',
                                        borderRadius: '10px',
                                        border: isSelected ? '2px solid #7c3aed' : '2px solid #e5e7eb',
                                        background: isSelected ? '#f5f0ff' : 'white',
                                        cursor: 'pointer',
                                        minWidth: '180px',
                                        transition: 'all 0.15s ease',
                                        boxShadow: isSelected ? '0 0 0 3px rgba(124,58,237,0.15)' : '0 1px 3px rgba(0,0,0,0.06)',
                                    }}
                                >
                                    <span style={{ fontWeight: 700, fontSize: '16px', color: isSelected ? '#7c3aed' : '#1a1a2e' }}>
                                        {c.displayName}
                                    </span>
                                    <span style={{ fontSize: '12px', color: '#888' }}>
                                        Nuevo inicio: <code style={{ background: '#f3f4f6', padding: '1px 5px', borderRadius: '3px' }}>{pilotPeriodo}</code>
                                    </span>
                                    <Badge
                                        appearance="filled"
                                        color={c.isPilotMode ? 'success' : 'informative'}
                                        style={{ fontSize: '10px' }}
                                    >
                                        {c.isPilotMode ? 'Piloto ON' : 'Piloto OFF'}
                                    </Badge>
                                </button>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Contenido principal */}
            {selectedCompany && (
                <>
                    {/* Banner de empresa */}
                    <div style={{
                        background: 'linear-gradient(135deg, #7c3aed 0%, #4f46e5 100%)',
                        borderRadius: '10px',
                        padding: '16px 24px',
                        marginBottom: '24px',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        color: 'white',
                    }}>
                        <div>
                            <div style={{ fontWeight: 700, fontSize: '18px' }}>{selectedCompany.displayName}</div>
                            <div style={{ fontSize: '13px', opacity: 0.85, marginTop: '2px' }}>
                                Período nuevo inicio: <strong>{periodoCodigo}</strong>
                                {' · '}Secciones actuales en piloto: <strong>{currentSections.length}</strong>
                            </div>
                        </div>
                        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                            <div style={{ fontSize: '12px', opacity: 0.8 }}>Cambiar período:</div>
                            <Input
                                id="periodo-input"
                                value={periodoCodigo}
                                onChange={(_, d) => setPeriodoCodigo(d.value)}
                                style={{ background: 'white', width: '120px', fontSize: '13px' }}
                                size="small"
                            />
                            <Button
                                id="reload-candidates-btn"
                                icon={<ArrowSyncRegular />}
                                appearance="secondary"
                                size="small"
                                onClick={handleReloadCandidates}
                                disabled={loadingCandidates}
                                style={{ background: 'rgba(255,255,255,0.15)', color: 'white', border: '1px solid rgba(255,255,255,0.3)' }}
                            >
                                Buscar
                            </Button>
                        </div>
                    </div>

                    {/* Dos paneles */}
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>

                        {/* Panel izquierdo: candidatos */}
                        <div style={{
                            background: 'white',
                            borderRadius: '10px',
                            boxShadow: '0 2px 8px rgba(0,0,0,0.07)',
                            overflow: 'hidden',
                        }}>
                            {/* Header del panel */}
                            <div style={{
                                padding: '16px 20px',
                                borderBottom: '1px solid #f0f0f0',
                                background: '#fafafa',
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                            }}>
                                <div>
                                    <div style={{ fontWeight: 700, fontSize: '15px', color: '#333' }}>
                                        Secciones Candidatas
                                    </div>
                                    <div style={{ fontSize: '12px', color: '#888', marginTop: '2px' }}>
                                        Periodo <code style={{ background: '#f3f4f6', padding: '1px 5px', borderRadius: '3px' }}>{periodoCodigo}</code>
                                        {' · '}{candidates.length} encontradas
                                    </div>
                                </div>
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    {selectedCandidates.size > 0 && (
                                        <Badge appearance="filled" color="brand">{selectedCandidates.size} sel.</Badge>
                                    )}
                                    <Button
                                        id="bulk-add-btn"
                                        icon={adding ? <Spinner size="tiny" /> : <AddCircleRegular />}
                                        appearance="primary"
                                        size="small"
                                        disabled={selectedCandidates.size === 0 || adding}
                                        onClick={handleBulkAdd}
                                    >
                                        {adding ? 'Agregando...' : `Agregar (${selectedCandidates.size})`}
                                    </Button>
                                </div>
                            </div>

                            {/* Buscador */}
                            <div style={{ padding: '10px 16px', borderBottom: '1px solid #f5f5f5' }}>
                                <Input
                                    id="candidate-filter-input"
                                    value={candidateFilter}
                                    onChange={(_, d) => setCandidateFilter(d.value)}
                                    placeholder="Filtrar por código o ID..."
                                    contentBefore={<SearchRegular />}
                                    style={{ width: '100%' }}
                                    size="small"
                                />
                            </div>

                            {/* Lista */}
                            <div style={{ maxHeight: '480px', overflowY: 'auto' }}>
                                {loadingCandidates ? (
                                    <div style={{ display: 'flex', justifyContent: 'center', padding: '40px' }}>
                                        <Spinner label="Cargando secciones..." />
                                    </div>
                                ) : filteredCandidates.length === 0 ? (
                                    <div style={{ padding: '32px', textAlign: 'center', color: '#888' }}>
                                        <DismissCircleRegular style={{ fontSize: '32px', display: 'block', margin: '0 auto 8px' }} />
                                        <div>No se encontraron secciones</div>
                                        <div style={{ fontSize: '12px', marginTop: '4px', color: '#aaa' }}>
                                            Verifica el código de periodo y vuelve a buscar
                                        </div>
                                    </div>
                                ) : (
                                    <>
                                        {/* Toggle all (solo nuevas) */}
                                        {newCandidates.length > 0 && (
                                            <div style={{
                                                padding: '8px 16px',
                                                borderBottom: '1px solid #f5f5f5',
                                                background: '#f9fafb',
                                                display: 'flex',
                                                alignItems: 'center',
                                                gap: '8px',
                                            }}>
                                                <Checkbox
                                                    id="select-all-checkbox"
                                                    checked={allNewSelected}
                                                    onChange={() => toggleAll(newCandidates)}
                                                    label={<span style={{ fontSize: '12px', color: '#555' }}>Seleccionar todas las nuevas ({newCandidates.length})</span>}
                                                />
                                            </div>
                                        )}
                                        {/* Candidatos nuevos */}
                                        {newCandidates.map(c => (
                                            <div key={c.idSeccion} style={{
                                                display: 'flex',
                                                alignItems: 'center',
                                                gap: '10px',
                                                padding: '9px 16px',
                                                borderBottom: '1px solid #f9f9f9',
                                                background: selectedCandidates.has(c.idSeccion) ? '#f0ebff' : 'white',
                                                transition: 'background 0.1s',
                                                cursor: 'pointer',
                                            }}
                                                onClick={() => toggleCandidate(c.idSeccion)}
                                            >
                                                <Checkbox
                                                    id={`cand-${c.idSeccion}`}
                                                    checked={selectedCandidates.has(c.idSeccion)}
                                                    onChange={() => toggleCandidate(c.idSeccion)}
                                                    onClick={e => e.stopPropagation()}
                                                />
                                                <div style={{ flex: 1 }}>
                                                    <div style={{ fontWeight: 600, fontSize: '13px', color: '#222' }}>{c.codigo}</div>
                                                    <div style={{ fontSize: '11px', color: '#888' }}>IdSeccion: {c.idSeccion}</div>
                                                </div>
                                            </div>
                                        ))}
                                        {/* Ya en piloto */}
                                        {alreadyInPilot.length > 0 && (
                                            <>
                                                <div style={{ padding: '6px 16px', background: '#e8f5e9', fontSize: '11px', color: '#388e3c', fontWeight: 600 }}>
                                                    ✓ Ya en el piloto ({alreadyInPilot.length})
                                                </div>
                                                {alreadyInPilot.map(c => (
                                                    <div key={c.idSeccion} style={{
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        gap: '10px',
                                                        padding: '9px 16px',
                                                        borderBottom: '1px solid #f9f9f9',
                                                        background: '#f6fff8',
                                                        opacity: 0.75,
                                                    }}>
                                                        <CheckmarkCircleRegular style={{ color: '#388e3c', fontSize: '16px' }} />
                                                        <div style={{ flex: 1 }}>
                                                            <div style={{ fontWeight: 600, fontSize: '13px', color: '#222' }}>{c.codigo}</div>
                                                            <div style={{ fontSize: '11px', color: '#888' }}>IdSeccion: {c.idSeccion}</div>
                                                        </div>
                                                        <Badge appearance="filled" color="success" style={{ fontSize: '10px' }}>en piloto</Badge>
                                                    </div>
                                                ))}
                                            </>
                                        )}
                                    </>
                                )}
                            </div>
                        </div>

                        {/* Panel derecho: secciones actuales del piloto */}
                        <div style={{
                            background: 'white',
                            borderRadius: '10px',
                            boxShadow: '0 2px 8px rgba(0,0,0,0.07)',
                            overflow: 'hidden',
                        }}>
                            <div style={{
                                padding: '16px 20px',
                                borderBottom: '1px solid #f0f0f0',
                                background: '#fafafa',
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                            }}>
                                <div>
                                    <div style={{ fontWeight: 700, fontSize: '15px', color: '#333' }}>
                                        Secciones en el Piloto
                                    </div>
                                    <div style={{ fontSize: '12px', color: '#888', marginTop: '2px' }}>
                                        Excluidas del SP v1
                                    </div>
                                </div>
                                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                    <Badge appearance="filled" color="informative">{currentSections.length} total</Badge>
                                    <Tooltip content="Recargar" relationship="label">
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

                            <div style={{ maxHeight: '540px', overflowY: 'auto' }}>
                                {loadingCurrent ? (
                                    <div style={{ display: 'flex', justifyContent: 'center', padding: '40px' }}>
                                        <Spinner label="Cargando piloto..." />
                                    </div>
                                ) : currentSections.length === 0 ? (
                                    <div style={{ padding: '32px', textAlign: 'center', color: '#888' }}>
                                        <BeakerRegular style={{ fontSize: '32px', display: 'block', margin: '0 auto 8px', color: '#ccc' }} />
                                        <div>No hay secciones en el piloto</div>
                                        <div style={{ fontSize: '12px', color: '#aaa', marginTop: '4px' }}>
                                            Selecciona candidatas y pulsa "Agregar"
                                        </div>
                                    </div>
                                ) : (
                                    <>
                                        {/* Resumen para el SP */}
                                        <div style={{
                                            margin: '12px 16px',
                                            padding: '10px 14px',
                                            background: '#f0f4ff',
                                            borderRadius: '6px',
                                            border: '1px solid #c7d7ff',
                                            fontSize: '12px',
                                            color: '#3730a3',
                                        }}>
                                            <div style={{ fontWeight: 700, marginBottom: '4px' }}>IDs para el SP (cTeamsPorSeccion):</div>
                                            <code style={{ wordBreak: 'break-all', lineHeight: 1.6 }}>
                                                {currentSections.map(s => `(${s.idSeccion})`).join(',\n')}
                                            </code>
                                        </div>

                                        {currentSections.map(s => (
                                            <div key={s.id} style={{
                                                display: 'flex',
                                                alignItems: 'center',
                                                gap: '10px',
                                                padding: '10px 16px',
                                                borderBottom: '1px solid #f9f9f9',
                                            }}>
                                                <CheckmarkCircleRegular style={{ color: '#7c3aed', fontSize: '16px', flexShrink: 0 }} />
                                                <div style={{ flex: 1 }}>
                                                    <div style={{ fontWeight: 600, fontSize: '13px', color: '#222' }}>
                                                        IdSeccion: <span style={{ color: '#7c3aed' }}>{s.idSeccion}</span>
                                                    </div>
                                                </div>
                                                <Tooltip content="Quitar del piloto" relationship="label">
                                                    <Button
                                                        id={`remove-${s.id}`}
                                                        icon={<DeleteRegular />}
                                                        appearance="subtle"
                                                        size="small"
                                                        onClick={() => handleRemove(s)}
                                                        style={{ color: '#d13438' }}
                                                    />
                                                </Tooltip>
                                            </div>
                                        ))}
                                    </>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Info box */}
                    <div style={{
                        marginTop: '24px',
                        padding: '14px 18px',
                        background: '#fffbeb',
                        border: '1px solid #fde68a',
                        borderRadius: '8px',
                        fontSize: '13px',
                        color: '#92400e',
                        display: 'flex',
                        gap: '10px',
                        alignItems: 'flex-start',
                    }}>
                        <span style={{ fontSize: '18px' }}>⚠️</span>
                        <div>
                            <strong>Recordatorio:</strong> Después de agregar las secciones aquí,
                            debes actualizar el bloque <code>@21</code> del SP <code>cTeamsPorSeccion</code> con los IDs mostrados
                            en el panel derecho. Los IDs del panel derecho corresponden al bloque
                            "<em>tercer piloto — periodo {periodoCodigo}</em>".
                            Este cambio en el SP <strong>no va a producción automáticamente</strong>.
                        </div>
                    </div>
                </>
            )}
        </div>
    );
};

export default PilotManagementPage;
