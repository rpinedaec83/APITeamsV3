import type { SelectTabData, TabValue } from '@fluentui/react-components';
import React, { useState, useMemo, useEffect } from 'react';
import * as XLSX from 'xlsx';
import {
    TabList,
    Tab,
    makeStyles,
    shorthands,
    Button,
    Input,
    Label,
    Title3,
    Divider,
    Table,
    TableHeader,
    TableRow,
    TableHeaderCell,
    TableBody,
    TableCell,
    tokens,
    Avatar,
    Badge,
    ProgressBar,
    Spinner,
} from '@fluentui/react-components';
import {
    SearchRegular,
    CheckmarkRegular,
    AddRegular,
    SaveRegular,
    CalendarRegular,
    ArrowUploadRegular,
    GridDotsRegular,
    ArrowSyncRegular,
    DeleteRegular,
    TriangleUpRegular,
    TriangleDownRegular,
    CopyRegular,
    VideoRegular,
} from '@fluentui/react-icons';
import { useMsal } from '@azure/msal-react';
import { useApiClient } from '../hooks/useApiClient';
import { showSuccess, showError, showConfirm, showWarning } from '../utils/alerts';
import Swal from 'sweetalert2';

interface Member {
    name: string;
    code: string;
    status: string;
}

interface SectionData {
    idSeccion: number;
    sede: string;
    producto: string;
    division: string;
    curso: string;
    programa: string;
    promocionCodigo?: string | null;
    promocionNombre?: string | null;
    semestre: string;
    profesor?: string; // Optional/Nullable in API
    teamTeacher?: string | null;
    replacementTeacher?: string | null;
    teacherReplacementStatus?: string | null;
    unidadNegocio: string;
    fechaInicio?: string | null;
    fechaFin?: string | null;
    linkGrabacion?: string | null;
    linkSharePoint?: string | null;
    sharePointTotalBytes?: number | null;
    sharePointUsedBytes?: number | null;
    sharePointRemainingBytes?: number | null;
    sharePointPercentAvailable?: number | null;
    hasTeam: boolean;
    esTeams: boolean; // Added to enforce academic flag check
    ineligibilityReason?: string;
    members: Member[];
}

interface EnrolledSection {
    sectionCode: string;
    courseName: string;
    teamStatus: string;
    studentStatus: string;
}

interface StudentData {
    sede: string;
    producto: string;
    division: string;
    seccion: string;
    programa: string;
    semestre: string;
    nombre: string;
    codigo: string;
    enrolledSections: EnrolledSection[];
}

type TeamActionProgress = 'refresh-members' | 'regenerate-agenda' | 'recreate-team' | 'transfer-recordings' | null;

const useStyles = makeStyles({
    root: {
        padding: '32px',
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        maxWidth: '1400px',
        margin: '0 auto',
        backgroundColor: tokens.colorNeutralBackground2,
        minHeight: '100vh',
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '16px',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px', '24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow4,
    },
    headerTitle: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        color: tokens.colorBrandForeground1,
    },
    mainCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow2,
        transition: 'transform 0.2s, box-shadow 0.2s',
        ':hover': {
            boxShadow: tokens.shadow8,
        },
    },
    panelContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
    },
    searchRegion: {
        display: 'flex',
        gap: '16px',
        alignItems: 'end',
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.padding('16px'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
    },
    inputGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        flexGrow: 1,
        maxWidth: '400px',
    },
    detailsGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: '20px',
        ...shorthands.padding('24px'),
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        marginTop: '16px',
    },
    detailItem: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    detailLabel: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        textTransform: 'uppercase',
        letterSpacing: '0.05em',
    },
    detailValue: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightBold,
        color: tokens.colorNeutralForeground1,
        wordBreak: 'break-word',
    },
    actionsRegion: {
        display: 'flex',
        gap: '12px',
        flexWrap: 'wrap',
        alignItems: 'center',
        marginTop: '24px',
        paddingTop: '20px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    bottomTabs: {
        marginTop: '32px',
    },
    tableContainer: {
        marginTop: '16px',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
        boxShadow: tokens.shadow2,
    },
    statusBadge: {
        textTransform: 'capitalize',
    },
    loadingOverlay: {
        position: 'fixed',
        top: 0,
        right: 0,
        bottom: 0,
        left: 0,
        backgroundColor: 'rgba(255, 255, 255, 0.75)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1040,
    },
    loadingOverlayContent: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('20px', '24px'),
        ...shorthands.borderRadius(tokens.borderRadiusLarge),
        boxShadow: tokens.shadow16,
        minWidth: '360px',
    },
    loadingProgressTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: '12px',
    },
    loadingProgressHint: {
        marginTop: '10px',
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
    },
});

const forceUtcDate = (value: string) => {
    return value.endsWith('Z') || value.includes('+') ? value : value + 'Z';
};

const formatDisplayDate = (value?: string | null) => {
    if (!value) return '';

    const date = new Date(forceUtcDate(value));
    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return new Intl.DateTimeFormat('es-PE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        timeZone: 'America/Lima'
    }).format(date);
};

const copyTextToClipboard = async (text: string) => {
    if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
        return;
    }

    const textArea = document.createElement('textarea');
    textArea.value = text;
    textArea.setAttribute('readonly', '');
    textArea.style.position = 'fixed';
    textArea.style.left = '-9999px';
    document.body.appendChild(textArea);
    textArea.select();

    const copied = document.execCommand('copy');
    document.body.removeChild(textArea);

    if (!copied) {
        throw new Error('No se pudo copiar el texto.');
    }
};

interface DriveQuotaResult {
    totalBytes: number;
    usedBytes: number;
    remainingBytes: number;
    percentAvailable: number;
    state: string;
    userPrincipalName: string;
    errorMessage: string;
    success: boolean;
}

const OperationsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();
    const { accounts } = useMsal();
    const recreateProvisioningWaitMs = 5 * 60 * 1000;

    const [loading, setLoading] = useState(false);
    const [storageQuota, setStorageQuota] = useState<DriveQuotaResult | null>(null);

    const fetchStorageQuota = async () => {
        try {
            const response = await apiClient.get<DriveQuotaResult>('/recordings/storage-quota');
            if (response.data && response.data.success) {
                setStorageQuota(response.data);
            } else {
                console.error(response.data?.errorMessage || 'Error al cargar almacenamiento');
            }
        } catch (err) {
            console.error('Error fetching storage quota:', err);
        }
    };

    useEffect(() => {
        fetchStorageQuota();
    }, []);
    const [teamActionInProgress, setTeamActionInProgress] = useState<TeamActionProgress>(null);
    const [error, setError] = useState('');

    // State for Top Tabs
    const [selectedTab, setSelectedTab] = useState<TabValue>('seccion');

    // State for Section Sub-tabs and Attendance
    const [seccionSubTab, setSeccionSubTab] = useState<'alumnos' | 'asistencia'>('alumnos');
    const [attendanceData, setAttendanceData] = useState<any>(null);
    const [loadingAttendance, setLoadingAttendance] = useState(false);
    const [selectedReportId, setSelectedReportId] = useState<string>('');
    const [attendanceSearch, setAttendanceSearch] = useState<string>('');

    const fetchAttendance = async () => {
        if (!seccionData?.idSeccion) return;
        setLoadingAttendance(true);
        try {
            const response = await apiClient.get(`/sections/${seccionData.idSeccion}/attendance`);
            setAttendanceData(response.data);
            setAttendanceSearch('');
            if (response.data?.reports && response.data.reports.length > 0) {
                setSelectedReportId(response.data.reports[0].id);
            }
        } catch (err: any) {
            console.error('Error fetching attendance:', err);
            const errMsg = err.response?.data?.message || 'No se pudo obtener el reporte de asistencia de la reunión.';
            showError(errMsg);
        } finally {
            setLoadingAttendance(false);
        }
    };


    // Form State (Seccion)
    const [seccionCodigo, setSeccionCodigo] = useState('');
    const [seccionData, setSeccionData] = useState<SectionData | null>(null);
    const [recentRecreate, setRecentRecreate] = useState<{ idSeccion: number; at: number } | null>(null);

    // Form State (Alumno)
    const [alumnoCodigo, setAlumnoCodigo] = useState('');
    const [alumnoData, setAlumnoData] = useState<StudentData | null>(null);

    // Grid State (Filtering & Sorting)
    const [gridSearch, setGridSearch] = useState('');
    const [sortConfig, setSortConfig] = useState<{ key: string; direction: 'ascending' | 'descending' }>({
        key: '',
        direction: 'ascending'
    });

    const handleSort = (key: string) => {
        setSortConfig(prev => ({
            key,
            direction: prev.key === key && prev.direction === 'ascending' ? 'descending' : 'ascending'
        }));
    };

    const teamActionProgressMessage = useMemo(() => {
        switch (teamActionInProgress) {
            case 'refresh-members':
                return 'Refrescando miembros del Team...';
            case 'regenerate-agenda':
                return 'Regenerando agendas del Team...';
            case 'recreate-team':
                return 'Recreando Team y aprovisionando en Microsoft 365...';
            case 'transfer-recordings':
                return 'Encolando transferencia de grabaciones...';
            default:
                return '';
        }
    }, [teamActionInProgress]);

    const processedData = useMemo(() => {
        if (selectedTab === 'seccion') {
            let members = [...(seccionData?.members || [])];
            
            // Filter
            if (gridSearch) {
                const term = gridSearch.toLowerCase();
                members = members.filter(m => 
                    (m.name || '').toLowerCase().includes(term) ||
                    (m.code || '').toLowerCase().includes(term) ||
                    (m.status || '').toLowerCase().includes(term)
                );
            }

            // Sort
            if (sortConfig.key) {
                members.sort((a: any, b: any) => {
                    const valA = a[sortConfig.key] || '';
                    const valB = b[sortConfig.key] || '';
                    const cmp = valA.toString().localeCompare(valB.toString());
                    return sortConfig.direction === 'ascending' ? cmp : -cmp;
                });
            }
            return members;
        } else {
            let sections = [...(alumnoData?.enrolledSections || [])];
            
            // Filter
            if (gridSearch) {
                const term = gridSearch.toLowerCase();
                sections = sections.filter(s => 
                    (s.courseName || '').toLowerCase().includes(term) ||
                    (s.sectionCode || '').toLowerCase().includes(term) ||
                    (s.teamStatus || '').toLowerCase().includes(term) ||
                    (s.studentStatus || '').toLowerCase().includes(term)
                );
            }

            // Sort
            if (sortConfig.key) {
                sections.sort((a: any, b: any) => {
                    const valA = a[sortConfig.key] || '';
                    const valB = b[sortConfig.key] || '';
                    const cmp = valA.toString().localeCompare(valB.toString());
                    return sortConfig.direction === 'ascending' ? cmp : -cmp;
                });
            }
            return sections;
        }
    }, [selectedTab, seccionData, alumnoData, gridSearch, sortConfig]);

    const handleSearchSeccion = async () => {
        if (!seccionCodigo) return;
        setLoading(true);
        setError('');
        setSeccionData(null);
        setSeccionSubTab('alumnos');
        setAttendanceData(null);
        setSelectedReportId('');
        try {
            const response = await apiClient.get(`/sections/search?code=${seccionCodigo}${isGestor ? '&skipSharePoint=true' : ''}`);
            setSeccionData(response.data);
            if (!response.data.esTeams) {
                await showWarning(
                    response.data.ineligibilityReason || 'Esta sección no cumple con los criterios para aprovisionamiento de Teams.',
                    'Sección no elegible'
                );
                setError(''); // Clear banner error if handled by SweetAlert
            }
        } catch (err: any) {
            if (err.response?.status === 404) {
                await showWarning(
                    `El código ${seccionCodigo} no existe en la base de datos de Académico o no ha sido procesado aún.`,
                    'Código no encontrado'
                );
            } else {
                setError(err.message || 'Error al buscar sección');
            }
        } finally {
            setLoading(false);
        }
    };

    const handleSearchAlumno = async () => {
        if (!alumnoCodigo) return;
        setLoading(true);
        setError('');
        setAlumnoData(null);
        try {
            const response = await apiClient.get(`/students/search?code=${alumnoCodigo}`);
            setAlumnoData(response.data);
        } catch (err: any) {
            if (err.response?.status === 404) {
                await showWarning(`No se encontró información para el código de alumno: ${alumnoCodigo}`, 'Alumno no encontrado');
            } else {
                const errorMessage = err.message || 'Error desconocido al buscar alumno';
                setError(errorMessage);
            }
        } finally {
            setLoading(false);
        }
    };

    const getCompanyKey = () => window.location.hostname.includes('zegel') ? 'zegel' : 'idat';
    const roles = useMemo(() => {
        const claims = accounts[0]?.idTokenClaims as any;
        if (!claims) return [];
        const rawRoles = (claims.roles || claims.role || claims.groups || []) as string | string[];
        const rolesArray = Array.isArray(rawRoles) ? rawRoles : [rawRoles];
        return rolesArray.map(r => r.toString().toUpperCase().trim());
    }, [accounts]);

    const canUseAdminItTeamActions = roles.includes('ADMIN') || roles.includes('IT');
    const isGestor = roles.some(r => r.includes('GESTION') || r.includes('GESTOR'));

    const ensureCanUseAdminItTeamActions = async () => {
        if (canUseAdminItTeamActions) {
            return true;
        }

        await showWarning('Esta acción está disponible solo para ADMIN o IT');
        return false;
    };

    const ensureAutomaticSyncNotRunning = async () => {
        try {
            const response = await apiClient.get('/sync/automatic-status');
            const isRunning = Boolean(
                response.data?.isAutomaticSyncRunning ??
                response.data?.IsAutomaticSyncRunning ??
                false
            );

            if (isRunning) {
                await showWarning('Se está ejecutando la sincronización automática');
                return false;
            }
        } catch {
            // If status cannot be verified, do not block manual operation.
        }

        return true;
    };

    const showProvisioningCompleted = async (summary?: string) => {
        const baseMessage = 'El proceso de aprovisionamiento ha finalizado. Puede continuar con las siguientes acciones.';
        const message = summary?.trim() ? `${baseMessage} ${summary.trim()}` : baseMessage;
        await showSuccess(message, 'Aprovisionamiento finalizado');
    };

    const handleProvisionTeam = async () => {
        if (!seccionData?.idSeccion) return;
        const result = await showConfirm('¿Estás seguro de crear/sincronizar el equipo para esta sección?');
        if (!result.isConfirmed) return;

        setLoading(true);
        try {
            const companyKey = getCompanyKey();
            const response = await apiClient.post(`/sync/section/${seccionData.idSeccion}?companyKey=${companyKey}`);
            const result = response.data;
            showSuccess(`Sincronización finalizada exitosamente. Equipos creados/actualizados: ${result.success}`);
            await handleSearchSeccion();
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error desconocido al iniciar sincronización';
            showError(errorMessage);
        } finally {
            setLoading(false);
        }
    };

    const handleRefreshMembers = async () => {
        if (!seccionData?.idSeccion) return;
        if (!await ensureCanUseAdminItTeamActions()) return;
        if (!await ensureAutomaticSyncNotRunning()) return;
        if (false && (
            recentRecreate?.idSeccion === seccionData?.idSeccion &&
            (Date.now() - (recentRecreate?.at ?? 0)) < recreateProvisioningWaitMs
        )) {
            await showWarning('El Team aún se está aprovisionando en Microsoft 365, intente en unos 5 minutos volver a dar clic en el botón ‘Regenerar agenda’.');
            return;
        }
        if (false && (
            recentRecreate?.idSeccion === seccionData?.idSeccion &&
            (Date.now() - (recentRecreate?.at ?? 0)) < recreateProvisioningWaitMs
        )) {
            await showWarning('El Team aún se está aprovisionando en Microsoft 365, intente en unos 5 minutos volver a dar clic en el botón ‘Regenerar agenda’.');
            return;
        }
        const result = await showConfirm('¿Estás seguro de refrescar alumnos y facilitadores para esta sección?');
        if (!result.isConfirmed) return;

        setTeamActionInProgress('refresh-members');
        setLoading(true);
        try {
            const companyKey = getCompanyKey();
            const response = await apiClient.post(`/sync/section/${seccionData.idSeccion}/members?companyKey=${companyKey}`);
            showSuccess(response.data.summary, 'Miembros actualizados');
            await handleSearchSeccion();
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error desconocido al refrescar miembros';
            showError(errorMessage);
        } finally {
            setTeamActionInProgress(null);
            setLoading(false);
        }
    };

    const handleVerifyTeam = async () => {
        if (!seccionData?.idSeccion) return;
        setLoading(true);
        try {
            const response = await apiClient.post(`/sync/verify/${seccionData.idSeccion}`);
            const result = response.data;
            setLoading(false);
            if (result.isValid) {
                await showSuccess(
                    result.summary || 'Se valido el Team y la informacion local quedo sincronizada.',
                    'Validacion de Team completada'
                );
            } else {
                await showWarning(
                    result.summary || 'Se detecto una inconsistencia durante la validacion del Team.',
                    'Inconsistencia detectada'
                );
            }
            await handleSearchSeccion();
        } catch (err: unknown) {
            setLoading(false);
            await showError('Error al validar el estado del Team.');
        }
    };

    const handleRegenerateAgenda = async () => {
        if (!seccionData?.idSeccion) return;
        if (!await ensureCanUseAdminItTeamActions()) return;
        if (!await ensureAutomaticSyncNotRunning()) return;
        const confirmResult = await showConfirm('Esto invalidará las reuniones pasadas y creará una nueva reunión de canal. ¿Proceder?');
        if (!confirmResult.isConfirmed) return;
        if (
            recentRecreate?.idSeccion === seccionData.idSeccion &&
            (Date.now() - recentRecreate.at) < recreateProvisioningWaitMs
        ) {
            await showWarning('El Team aun se esta aprovisionando en Microsoft 365, intente en unos 5 minutos volver a dar clic en el boton "Regenerar agenda".');
            return;
        }
        setTeamActionInProgress('regenerate-agenda');
        setLoading(true);
        try {
            const companyKey = getCompanyKey();
            const response = await apiClient.post(`/sync/agenda/regenerate/${seccionData.idSeccion}?companyKey=${companyKey}`);
            showSuccess(response.data.summary, 'Agenda regenerada');
        } catch (err: unknown) {
            showError('Error al regenerar agenda.');
        } finally {
            setTeamActionInProgress(null);
            setLoading(false);
        }
    };

    const handleRecreateTeam = async () => {
        if (!seccionData?.idSeccion) return;
        if (!await ensureCanUseAdminItTeamActions()) return;
        if (!await ensureAutomaticSyncNotRunning()) return;

        // Step 1: Initial warning
        const step1 = await Swal.fire({
            icon: 'warning',
            title: '⚠️ Acción destructiva',
            html: `
                <p>Esta acción <b>ELIMINARÁ permanentemente</b> el equipo de Microsoft Teams para la sección <b>${seccionData.idSeccion}</b>.</p>
                <ul style="text-align:left; margin-top:12px; color:#666;">
                    <li>Se borrarán todos los mensajes y archivos</li>
                    <li>Se eliminarán las agendas asociadas</li>
                    <li>Se creará un equipo completamente nuevo</li>
                </ul>
            `,
            showCancelButton: true,
            confirmButtonColor: '#d13438',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Sí, quiero continuar',
            cancelButtonText: 'Cancelar',
        });
        if (!step1.isConfirmed) return;

        // Step 2: Type confirmation
        const step2 = await Swal.fire({
            icon: 'error',
            title: 'Confirmación final',
            html: '<p>Escribe <b>RECREAR</b> para confirmar la eliminación y recreación del equipo:</p>',
            input: 'text',
            inputPlaceholder: 'Escribe RECREAR aquí',
            showCancelButton: true,
            confirmButtonColor: '#d13438',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Recrear equipo',
            cancelButtonText: 'Cancelar',
            inputValidator: (value: string) => {
                if (value !== 'RECREAR') {
                    return 'Debes escribir RECREAR exactamente para continuar';
                }
                return null;
            },
        });
        if (!step2.isConfirmed) return;

        let provisioningCompleted = false;
        setTeamActionInProgress('recreate-team');
        setLoading(true);
        try {
            const companyKey = getCompanyKey();
            const response = await apiClient.post(`/sync/recreate/${seccionData.idSeccion}?companyKey=${companyKey}`);
            showSuccess(response.data.summary, 'Equipo recreado');
            await handleSearchSeccion();
            setRecentRecreate({ idSeccion: seccionData.idSeccion, at: Date.now() });
            provisioningCompleted = true;
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error al recrear equipo.';
            showError(errorMessage);
        } finally {
            setTeamActionInProgress(null);
            setLoading(false);
        }

        if (provisioningCompleted) {
            await showProvisioningCompleted();
        }
    };

    const handleTransferRecordings = async () => {
        if (!seccionData?.idSeccion) return;
        if (!await ensureCanUseAdminItTeamActions()) return;
        
        const result = await showConfirm('¿Estás seguro de encolar la transferencia de grabaciones para esta sección?');
        if (!result.isConfirmed) return;

        setTeamActionInProgress('transfer-recordings');
        setLoading(true);
        try {
            const response = await apiClient.post(`/jobs/recordings-transfer/${seccionData.idSeccion}`);
            showSuccess(response.data.message || 'Transferencia encolada correctamente', 'Job Encolado');
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error al encolar transferencia de grabaciones.';
            showError(errorMessage);
        } finally {
            setTeamActionInProgress(null);
            setLoading(false);
        }
    };

    const handleSyncAlumno = async () => {
        const codigo = (alumnoData?.codigo || alumnoCodigo || '').trim();
        if (!codigo) return;
        setLoading(true);
        try {
            const response = await apiClient.post(`/sync/student/${codigo}`);
            showSuccess(`Sincronización de alumno encolada. ${response.data.message || ''}`);
        } catch (err: unknown) {
            showError('Error al sincronizar alumno.');
        } finally {
            setLoading(false);
        }
    };

    const handleExportReport = () => {
        if (selectedTab === 'seccion' && seccionData) {
            const wb = XLSX.utils.book_new();
            
            const metadata = [
                ['Reporte de Operaciones - Sección'],
                ['Fecha de exportación:', new Date().toLocaleString()],
                [''],
                ['DETALLES DE LA SECCIÓN'],
                ['Sede', seccionData.sede],
                ['Producto', seccionData.producto],
                ['División', seccionData.division],
                ['Curso', seccionData.curso],
                ['Programa', seccionData.programa],
                ['Semestre', seccionData.semestre],
                ['Inicio del curso', formatDisplayDate(seccionData.fechaInicio) || 'N/A'],
                ['Fin del curso', formatDisplayDate(seccionData.fechaFin) || 'N/A'],
                ['Facilitador Académico', seccionData.profesor || 'N/A'],
                ['Docente en TeamsEquipos', seccionData.teamTeacher || 'N/A'],
                ['Docente a Reemplazar', seccionData.replacementTeacher || 'N/A'],
                ['Estado Docente', seccionData.teacherReplacementStatus || 'N/A'],
                ['Unidad Negocio', seccionData.unidadNegocio],
                ['Link de clases', seccionData.linkGrabacion || 'N/A'],
                [''],
                ['LISTADO DE ALUMNOS'],
                ['#', 'Código', 'Nombre', 'Estado']
            ];

            const dataRows = seccionData.members.map((m, i) => [
                i + 1,
                m.code,
                m.name,
                m.status
            ]);

            const ws = XLSX.utils.aoa_to_sheet([...metadata, ...dataRows]);
            ws['!cols'] = [{ wch: 5 }, { wch: 15 }, { wch: 45 }, { wch: 15 }];

            XLSX.utils.book_append_sheet(wb, ws, 'Reporte');
            XLSX.writeFile(wb, `Reporte_Seccion_${seccionCodigo || 'Export'}.xlsx`);
        } else if (selectedTab === 'alumno' && alumnoData) {
            const wb = XLSX.utils.book_new();
            
            const metadata = [
                ['Reporte de Operaciones - Alumno'],
                ['Fecha de exportación:', new Date().toLocaleString()],
                [''],
                ['DETALLES DEL ALUMNO'],
                ['Nombre', alumnoData.nombre],
                ['Código', alumnoData.codigo],
                ['Sede', alumnoData.sede],
                ['Producto', alumnoData.producto],
                ['División', alumnoData.division],
                ['Programa', alumnoData.programa],
                ['Semestre', alumnoData.semestre],
                [''],
                ['CURSOS MATRICULADOS'],
                ['#', 'Código de Sección', 'Nombre del Curso', 'Estado Team', 'Estado Alumno']
            ];

            const dataRows = alumnoData.enrolledSections.map((s, i) => [
                i + 1,
                s.sectionCode,
                s.courseName,
                s.teamStatus,
                s.studentStatus
            ]);

            const ws = XLSX.utils.aoa_to_sheet([...metadata, ...dataRows]);
            ws['!cols'] = [{ wch: 5 }, { wch: 20 }, { wch: 45 }, { wch: 15 }, { wch: 15 }];

            XLSX.utils.book_append_sheet(wb, ws, 'Reporte');
            XLSX.writeFile(wb, `Reporte_Alumno_${alumnoCodigo || 'Export'}.xlsx`);
        }
    };



    const onTabSelect = (_: unknown, data: SelectTabData) => {
        if (data?.value) {
            setSelectedTab(data.value);
        }
    };

    const handleCopyLinkGrabacion = async (link?: string | null, source: 'class' | 'sharepoint' = 'class') => {
        if (!link) return;

        try {
            await copyTextToClipboard(link);
            if (source === 'sharepoint') {
                showSuccess('El link de SharePoint fue copiado al portapapeles.', 'Link copiado');
            } else {
                showSuccess('El link de clases fue copiado al portapapeles.', 'Link copiado');
            }
        } catch {
            if (source === 'sharepoint') {
                showError('No se pudo copiar el link de SharePoint.');
            } else {
                showError('No se pudo copiar el link de clases.');
            }
        }
    };

    return (
        <div className={styles.root}>
            {loading && (
                <div className={styles.loadingOverlay}>
                    <div className={styles.loadingOverlayContent}>
                        {teamActionInProgress ? (
                            <>
                                <div className={styles.loadingProgressTitle}>{teamActionProgressMessage}</div>
                                <ProgressBar thickness="large" />
                                <div className={styles.loadingProgressHint}>Espere por favor, el proceso puede tardar unos minutos.</div>
                            </>
                        ) : (
                            <Spinner label="Procesando..." labelPosition="below" size="extra-large" />
                        )}
                    </div>
                </div>
            )}
            <div className={styles.header}>
                <div className={styles.headerTitle}>
                    <Avatar color="brand" icon={<GridDotsRegular />} size={48} />
                    <div>
                        <Title3>Dashboard de Operaciones</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>Gestión de Aprovisionamiento de Teams</div>
                    </div>
                </div>

                {storageQuota && (
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '12px',
                        backgroundColor: tokens.colorNeutralBackground2,
                        padding: '8px 16px',
                        borderRadius: tokens.borderRadiusMedium,
                        border: `1px solid ${tokens.colorNeutralStroke2}`,
                        fontSize: '13px',
                    }}>
                        <VideoRegular style={{ color: tokens.colorBrandForeground1, fontSize: '18px' }} />
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                            <div style={{ fontWeight: 'semibold', display: 'flex', gap: '8px', alignItems: 'center' }}>
                                <span>Grabaciones (OneDrive):</span>
                                <span style={{ 
                                    color: storageQuota.percentAvailable < 15 ? tokens.colorPaletteRedForeground1 : tokens.colorBrandForeground1,
                                    fontWeight: 'bold' 
                                }}>
                                    {((storageQuota.remainingBytes) / (1024 * 1024 * 1024)).toFixed(1)} GB libres ({storageQuota.percentAvailable.toFixed(1)}%)
                                </span>
                            </div>
                            <div style={{ display: 'flex', gap: '12px', color: tokens.colorNeutralForeground2, fontSize: '11px' }}>
                                <span>Total: {((storageQuota.totalBytes) / (1024 * 1024 * 1024)).toFixed(1)} GB</span>
                                <span>•</span>
                                <span>Usado: {((storageQuota.usedBytes) / (1024 * 1024 * 1024)).toFixed(1)} GB</span>
                                {storageQuota.userPrincipalName && (
                                    <>
                                        <span>•</span>
                                        <span style={{ textTransform: 'lowercase' }}>{storageQuota.userPrincipalName}</span>
                                    </>
                                )}
                            </div>
                        </div>
                    </div>
                )}

                <div style={{ display: 'flex', gap: '10px' }}>
                    <Button 
                        icon={<ArrowUploadRegular />} 
                        onClick={handleExportReport}
                        disabled={selectedTab === 'seccion' ? !seccionData : !alumnoData}
                        appearance="outline"
                    >
                        Exportar Reporte
                    </Button>
                    <Button appearance="subtle">Ayuda</Button>
                </div>
            </div>

            {/* Main Tabs (Seccion / Alumno) */}
            <div className={styles.mainCard}>
                <TabList selectedValue={selectedTab} onTabSelect={onTabSelect} size="large">
                    <Tab value="seccion">Por Sección</Tab>
                    <Tab value="alumno">Por Alumno</Tab>
                </TabList>

                <Divider style={{ margin: '16px 0' }} />

                {/* Panel: Por Sección */}
                {selectedTab === 'seccion' && (
                    <div className={styles.panelContainer}>

                        <div className={styles.searchRegion}>
                            <div className={styles.inputGroup}>
                                <Label weight="semibold">Código de Horario</Label>
                                <Input
                                    placeholder="Ej. 12345"
                                    size="large"
                                    value={seccionCodigo}
                                    onChange={(_e, d) => setSeccionCodigo(d.value)}
                                    onKeyDown={(e) => e.key === 'Enter' && handleSearchSeccion()}
                                />
                            </div>
                            <Button icon={<SearchRegular />} appearance="primary" size="large" onClick={handleSearchSeccion} disabled={loading}>
                                {loading ? 'Buscando...' : 'Buscar'}
                            </Button>
                            <Button icon={<CheckmarkRegular />} size="large" onClick={handleVerifyTeam} disabled={loading || !seccionData}>
                                Validar estado del Team
                            </Button>
                        </div>

                        {error && (
                            <div style={{ color: tokens.colorPaletteRedForeground1, padding: '10px', backgroundColor: tokens.colorPaletteRedBackground2, borderRadius: tokens.borderRadiusMedium }}>
                                {error}
                            </div>
                        )}

                        {/* Details Section */}
                        {seccionData && (
                            <div className={styles.detailsGrid}>
                                <DetailItem label="Sede" value={seccionData.sede} />
                                <DetailItem label="Producto" value={seccionData.producto} />
                                <DetailItem label="División" value={seccionData.division} />
                                <DetailItem label="Curso" value={seccionData.curso} />
                                <DetailItem label="Programa" value={seccionData.programa} />
                                <DetailItem label="Promoción código" value={seccionData.promocionCodigo || 'N/A'} />
                                <DetailItem label="Promoción nombre" value={seccionData.promocionNombre || 'N/A'} />
                                <DetailItem label="Semestre" value={seccionData.semestre} />
                                <DetailItem label="Inicio del Curso" value={formatDisplayDate(seccionData.fechaInicio)} />
                                <DetailItem label="Fin del Curso" value={formatDisplayDate(seccionData.fechaFin)} />
                                <DetailItem label="Facilitador Académico" value={seccionData.profesor || 'N/A'} />
                                <DetailItem label="Docente en TeamsEquipos" value={seccionData.teamTeacher || 'N/A'} />
                                <DetailItem label="Docente a Reemplazar" value={seccionData.replacementTeacher || 'N/A'} />
                                <DetailItem label="Estado Docente" value={seccionData.teacherReplacementStatus || 'N/A'} />
                                <DetailItem label="Unidad Negocio" value={seccionData.unidadNegocio} />
                                <CopyDetailItem
                                    label="Link de clases"
                                    value={seccionData.linkGrabacion}
                                    sharePointUrl={seccionData.linkSharePoint}
                                    onCopy={handleCopyLinkGrabacion}
                                    hideSharePoint={isGestor}
                                />
                            </div>
                        )}

                        {seccionData && (
                            <div className={styles.actionsRegion}>
                                {seccionData.hasTeam ? (
                                    <>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '16px', flexWrap: 'wrap' }}>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: tokens.colorPaletteGreenForeground1 }}>
                                                <CheckmarkRegular fontSize={24} />
                                                <Title3>Equipo Activo</Title3>
                                            </div>
                                            {seccionData.sharePointTotalBytes !== undefined && seccionData.sharePointTotalBytes !== null && (
                                                <div style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '12px',
                                                    backgroundColor: tokens.colorNeutralBackground2,
                                                    padding: '8px 16px',
                                                    borderRadius: tokens.borderRadiusMedium,
                                                    border: `1px solid ${tokens.colorNeutralStroke2}`,
                                                    fontSize: '13px',
                                                }}>
                                                    <VideoRegular style={{ color: tokens.colorBrandForeground1, fontSize: '18px' }} />
                                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                                                        <div style={{ fontWeight: 'semibold', display: 'flex', gap: '8px', alignItems: 'center' }}>
                                                            <span>Almacenamiento (SharePoint):</span>
                                                            <span style={{ 
                                                                color: (seccionData.sharePointPercentAvailable ?? 100) < 15 ? tokens.colorPaletteRedForeground1 : tokens.colorBrandForeground1,
                                                                fontWeight: 'bold' 
                                                            }}>
                                                                {((seccionData.sharePointRemainingBytes ?? 0) / (1024 * 1024 * 1024)).toFixed(1)} GB libres ({seccionData.sharePointPercentAvailable?.toFixed(1)}%)
                                                            </span>
                                                        </div>
                                                        <div style={{ display: 'flex', gap: '12px', color: tokens.colorNeutralForeground2, fontSize: '11px' }}>
                                                            <span>Total: {((seccionData.sharePointTotalBytes ?? 0) / (1024 * 1024 * 1024)).toFixed(1)} GB</span>
                                                            <span>•</span>
                                                            <span>Usado: {((seccionData.sharePointUsedBytes ?? 0) / (1024 * 1024 * 1024)).toFixed(1)} GB</span>
                                                        </div>
                                                    </div>
                                                </div>
                                            )}
                                        </div>
                                        <span style={{ flex: 1 }}></span>
                                        <Button
                                            icon={<SaveRegular />}
                                            onClick={handleRefreshMembers}
                                            disabled={loading || !canUseAdminItTeamActions}
                                            title={!canUseAdminItTeamActions ? 'Disponible solo para ADMIN o IT' : undefined}
                                        >
                                            Refrescar Miembros
                                        </Button>
                                        <Button
                                            icon={<VideoRegular />}
                                            onClick={handleTransferRecordings}
                                            disabled={loading || !canUseAdminItTeamActions}
                                            title={!canUseAdminItTeamActions ? 'Disponible solo para ADMIN o IT' : undefined}
                                        >
                                            Transferir Grabaciones
                                        </Button>
                                        <Button
                                            icon={<CalendarRegular />}
                                            onClick={handleRegenerateAgenda}
                                            disabled={loading || !canUseAdminItTeamActions}
                                            title={!canUseAdminItTeamActions ? 'Disponible solo para ADMIN o IT' : undefined}
                                        >
                                            Regenerar Agendas
                                        </Button>
                                        <Button
                                            icon={<ArrowSyncRegular />}
                                            onClick={handleRecreateTeam}
                                            disabled={loading || !canUseAdminItTeamActions}
                                            title={!canUseAdminItTeamActions ? 'Disponible solo para ADMIN o IT' : undefined}
                                            style={{ color: tokens.colorPaletteRedForeground1, borderColor: tokens.colorPaletteRedBorderActive }}
                                        >
                                            Recrear Equipo
                                        </Button>
                                    </>
                                ) : (
                                    <>
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: tokens.colorPaletteRedForeground1 }}>
                                                <Title3>No existe Equipo</Title3>
                                            </div>
                                            <div style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground2 }}>
                                                Esta sección no tiene un equipo de Teams vinculado en la base de datos local.
                                            </div>
                                        </div>
                                        <span style={{ flex: 1 }}></span>
                                        <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
                                            <Button
                                                appearance="secondary"
                                                size="large"
                                                icon={<DeleteRegular />}
                                                onClick={() => {
                                                    setSeccionData(null);
                                                    setError('');
                                                    setSeccionCodigo('');
                                                }}
                                                style={{ 
                                                    fontWeight: '600',
                                                    padding: '0 24px',
                                                    borderColor: tokens.colorNeutralStroke1,
                                                    transition: 'all 0.2s ease',
                                                }}
                                            >
                                                Limpiar
                                            </Button>
                                            <Button
                                                appearance="primary"
                                                size="large"
                                                icon={<AddRegular />}
                                                style={{ 
                                                    background: `linear-gradient(135deg, ${tokens.colorBrandBackground} 0%, #4B53E7 100%)`,
                                                    color: 'white',
                                                    fontWeight: '600',
                                                    padding: '0 32px',
                                                    boxShadow: '0 4px 14px 0 rgba(75, 83, 231, 0.39)',
                                                    transition: 'all 0.2s ease',
                                                }}
                                                onClick={handleProvisionTeam}
                                                disabled={loading || !seccionData.esTeams}
                                            >
                                                {loading ? 'Aprovisionando...' : 'Aprovisionar Equipo en Teams'}
                                            </Button>
                                        </div>
                                    </>
                                )}
                            </div>
                        )}

                    </div>
                )}

                {/* Panel: Por Alumno */}
                {selectedTab === 'alumno' && (
                    <div className={styles.panelContainer}>

                        <div className={styles.searchRegion}>
                            <div className={styles.inputGroup}>
                                <Label weight="semibold">Código de Alumno</Label>
                                <Input
                                    placeholder="Ej. U20201234"
                                    size="large"
                                    value={alumnoCodigo}
                                    onChange={(_e, d) => setAlumnoCodigo(d.value)}
                                    onKeyDown={(e) => e.key === 'Enter' && handleSearchAlumno()}
                                />
                            </div>
                            <Button icon={<SearchRegular />} appearance="primary" size="large" onClick={handleSearchAlumno} disabled={loading}>
                                {loading ? 'Buscando...' : 'Buscar'}
                            </Button>
                        </div>

                        {error && (
                            <div style={{ color: tokens.colorPaletteRedForeground1, padding: '10px', backgroundColor: tokens.colorPaletteRedBackground2, borderRadius: tokens.borderRadiusMedium }}>
                                {error}
                            </div>
                        )}

                        {/* Details Section */}
                        {alumnoData && (
                            <div className={styles.detailsGrid}>
                                <DetailItem label="Sede" value={alumnoData.sede} />
                                <DetailItem label="Producto" value={alumnoData.producto} />
                                <DetailItem label="División" value={alumnoData.division} />
                                <DetailItem label="Sección (Principal)" value={alumnoData.seccion} />
                                <DetailItem label="Programa" value={alumnoData.programa} />
                                <DetailItem label="Semestre" value={alumnoData.semestre} />
                                <DetailItem label="Alumno" value={`${alumnoData.nombre} (${alumnoData.codigo})`} />
                            </div>
                        )}

                        {alumnoData && alumnoData.enrolledSections && alumnoData.enrolledSections.length > 0 && (
                            <div style={{ padding: '0 24px' }}>
                                <Title3>Cursos Matriculados</Title3>
                                <ul>
                                    {alumnoData.enrolledSections.map((c, i) => <li key={i}>{c.courseName} ({c.sectionCode})</li>)}
                                </ul>
                            </div>
                        )}

                        <div className={styles.actionsRegion}>
                            <span style={{ flex: 1 }}></span>
                            <Button icon={<AddRegular />} onClick={() => {
                                setAlumnoData(null);
                                setError('');
                                setAlumnoCodigo('');
                            }}>Limpiar</Button>
                            <Button icon={<SaveRegular />} appearance="primary" onClick={handleSyncAlumno} disabled={loading || !alumnoCodigo.trim()}>Actualizar Teams</Button>
                        </div>

                    </div>
                )}
            </div>

            {/* Bottom Data Grids */}
            <div className={styles.bottomTabs}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px', flexWrap: 'wrap', gap: '16px' }}>
                    <Title3>Resultados y Detalles</Title3>
                    {selectedTab === 'seccion' && seccionData && (
                        <TabList
                            selectedValue={seccionSubTab}
                            onTabSelect={(_e, d) => {
                                const val = d.value as 'alumnos' | 'asistencia';
                                setSeccionSubTab(val);
                                if (val === 'asistencia' && !attendanceData) {
                                    fetchAttendance();
                                }
                            }}
                            appearance="subtle"
                        >
                            <Tab value="alumnos">Listado de Alumnos</Tab>
                            <Tab value="asistencia">Asistencia de Reunión</Tab>
                        </TabList>
                    )}
                </div>
                <div className={styles.tableContainer}>
                    {selectedTab === 'seccion' && seccionSubTab === 'asistencia' ? (
                        loadingAttendance ? (
                            <div style={{ padding: '40px', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
                                <Spinner label="Cargando asistencia desde Teams..." />
                            </div>
                        ) : !attendanceData || !attendanceData.reports || attendanceData.reports.length === 0 ? (
                            <div style={{ padding: '40px', textAlign: 'center', color: tokens.colorNeutralForeground2 }}>
                                <VideoRegular fontSize={48} style={{ marginBottom: '12px' }} />
                                <div>No se encontraron reportes de asistencia para esta sección en Teams.</div>
                                <div style={{ fontSize: '12px', marginTop: '4px' }}>Esto puede deberse a que la reunión virtual aún no se ha iniciado o no tiene participantes registrados.</div>
                            </div>
                        ) : (
                            <div>
                                <div style={{ padding: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: tokens.colorNeutralBackground3, borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                                    <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
                                        <Label style={{ fontWeight: '600' }}>Reuniones Realizadas:</Label>
                                        <select
                                            value={selectedReportId}
                                            onChange={(e) => setSelectedReportId(e.target.value)}
                                            style={{
                                                padding: '6px 12px',
                                                borderRadius: tokens.borderRadiusMedium,
                                                border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                backgroundColor: tokens.colorNeutralBackground1,
                                                fontSize: '13px',
                                                cursor: 'pointer',
                                                minWidth: '280px'
                                            }}
                                        >
                                            {attendanceData.reports.map((report: any) => (
                                                <option key={report.id} value={report.id}>
                                                    Sesión del {formatDateTime(report.meetingStartDateTime)} ({report.totalParticipantCount} asistentes)
                                                </option>
                                            ))}
                                        </select>
                                    </div>
                                    <Input
                                        placeholder="Buscar por nombre o correo..."
                                        size="small"
                                        contentBefore={<SearchRegular />}
                                        value={attendanceSearch}
                                        onChange={(_e, d) => setAttendanceSearch(d.value)}
                                        style={{ minWidth: '280px' }}
                                    />
                                </div>
                                
                                {(() => {
                                    const selectedReport = attendanceData.reports.find((r: any) => r.id === selectedReportId);
                                    if (!selectedReport) return null;

                                    const records = selectedReport.attendanceRecords || [];
                                    const totalParticipants = selectedReport.totalParticipantCount || records.length;
                                    const organizersCount = records.filter((r: any) => r.role === 'Organizer').length;
                                    const attendeesCount = records.filter((r: any) => r.role === 'Attendee' || r.role !== 'Organizer').length;
                                    
                                    const averageDurationSeconds = records.length > 0 
                                        ? Math.round(records.reduce((acc: number, curr: any) => acc + (curr.totalAttendanceInSeconds || 0), 0) / records.length)
                                        : 0;

                                    const filteredRecords = records.filter((r: any) => {
                                        if (!attendanceSearch) return true;
                                        const search = attendanceSearch.toLowerCase();
                                        return (r.displayName?.toLowerCase().includes(search) || 
                                                r.emailAddress?.toLowerCase().includes(search) || 
                                                r.role?.toLowerCase().includes(search));
                                    });

                                    return (
                                        <>
                                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', padding: '16px', backgroundColor: tokens.colorNeutralBackground2, borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                                                <div style={{ backgroundColor: tokens.colorNeutralBackground1, padding: '12px 16px', borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke1}`, display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                    <span style={{ fontSize: '11px', color: tokens.colorNeutralForeground2, fontWeight: '600', textTransform: 'uppercase' }}>Total Participantes</span>
                                                    <span style={{ fontSize: '20px', fontWeight: 'bold', color: tokens.colorBrandForegroundLink }}>{totalParticipants}</span>
                                                </div>
                                                <div style={{ backgroundColor: tokens.colorNeutralBackground1, padding: '12px 16px', borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke1}`, display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                    <span style={{ fontSize: '11px', color: tokens.colorNeutralForeground2, fontWeight: '600', textTransform: 'uppercase' }}>Docentes / Organizadores</span>
                                                    <span style={{ fontSize: '20px', fontWeight: 'bold', color: tokens.colorPaletteBlueForeground2 }}>{organizersCount}</span>
                                                </div>
                                                <div style={{ backgroundColor: tokens.colorNeutralBackground1, padding: '12px 16px', borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke1}`, display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                    <span style={{ fontSize: '11px', color: tokens.colorNeutralForeground2, fontWeight: '600', textTransform: 'uppercase' }}>Asistentes / Alumnos</span>
                                                    <span style={{ fontSize: '20px', fontWeight: 'bold', color: tokens.colorPaletteGreenForeground1 }}>{attendeesCount}</span>
                                                </div>
                                                <div style={{ backgroundColor: tokens.colorNeutralBackground1, padding: '12px 16px', borderRadius: tokens.borderRadiusMedium, border: `1px solid ${tokens.colorNeutralStroke1}`, display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                    <span style={{ fontSize: '11px', color: tokens.colorNeutralForeground2, fontWeight: '600', textTransform: 'uppercase' }}>Tiempo Promedio</span>
                                                    <span style={{ fontSize: '20px', fontWeight: 'bold', color: tokens.colorPalettePurpleForeground2 }}>{formatDuration(averageDurationSeconds)}</span>
                                                </div>
                                            </div>

                                            <Table>
                                                <TableHeader>
                                                    <TableRow>
                                                        <TableHeaderCell style={{ width: '40px' }}>#</TableHeaderCell>
                                                        <TableHeaderCell style={{ fontWeight: '600' }}>Nombre / Correo</TableHeaderCell>
                                                        <TableHeaderCell style={{ fontWeight: '600' }}>Rol</TableHeaderCell>
                                                        <TableHeaderCell style={{ fontWeight: '600' }}>Tiempo Total</TableHeaderCell>
                                                        <TableHeaderCell style={{ fontWeight: '600' }}>Inicio / Fin</TableHeaderCell>
                                                    </TableRow>
                                                </TableHeader>
                                                <TableBody>
                                                    {filteredRecords.length > 0 ? (
                                                        filteredRecords.map((record: any, idx: number) => (
                                                            <TableRow key={idx}>
                                                                <TableCell>{idx + 1}</TableCell>
                                                                <TableCell>
                                                                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                                        <Avatar name={record.displayName || 'Invitado'} size={24} color="colorful" />
                                                                        <div style={{ display: 'flex', flexDirection: 'column' }}>
                                                                            <span style={{ fontWeight: 600 }}>{record.displayName || 'Invitado'}</span>
                                                                            <span style={{ fontSize: '11px', color: tokens.colorNeutralForeground2 }}>{record.emailAddress || 'Sin correo registrado'}</span>
                                                                        </div>
                                                                    </div>
                                                                </TableCell>
                                                                <TableCell>
                                                                    <Badge appearance="outline" color={record.role === 'Organizer' ? 'brand' : 'informative'}>
                                                                        {record.role === 'Organizer' ? 'Organizador' : record.role === 'Presenter' ? 'Presentador' : 'Asistente'}
                                                                    </Badge>
                                                                </TableCell>
                                                                <TableCell style={{ fontWeight: '600' }}>
                                                                    {formatDuration(record.totalAttendanceInSeconds)}
                                                                </TableCell>
                                                                <TableCell>
                                                                    {record.intervals && record.intervals.length > 0 ? (
                                                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                                                            {record.intervals.map((interval: any, i: number) => (
                                                                                <span key={i} style={{ fontSize: '12px', whiteSpace: 'nowrap' }}>
                                                                                    {formatTimeOnly(interval.joinDateTime)} – {formatTimeOnly(interval.leaveDateTime)}
                                                                                </span>
                                                                            ))}
                                                                        </div>
                                                                    ) : record.firstJoinDateTime || record.lastLeaveDateTime ? (
                                                                        <span style={{ fontSize: '12px', whiteSpace: 'nowrap' }}>
                                                                            {formatTimeOnly(record.firstJoinDateTime)} – {formatTimeOnly(record.lastLeaveDateTime)}
                                                                        </span>
                                                                    ) : (
                                                                        <span style={{ color: tokens.colorNeutralForeground4 }}>-</span>
                                                                    )}
                                                                </TableCell>
                                                            </TableRow>
                                                        ))
                                                    ) : (
                                                        <TableRow>
                                                            <TableCell colSpan={5} style={{ textAlign: 'center', padding: '20px' }}>No hay registros de asistencia que coincidan con la búsqueda.</TableCell>
                                                        </TableRow>
                                                    )}
                                                </TableBody>
                                            </Table>
                                        </>
                                    );
                                })()}
                            </div>
                        )
                    ) : (
                        <>
                            <div style={{ padding: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <div style={{ fontWeight: 600, color: tokens.colorNeutralForeground1 }}>
                                    {selectedTab === 'seccion' ? `Listado de Alumnos (${processedData.length || 0})` : `Cursos Matriculados (${processedData.length || 0})`}
                                </div>
                                <Input
                                    placeholder="Filtrar por nombre, código o estado..."
                                    size="small"
                                    contentBefore={<SearchRegular />}
                                    value={gridSearch}
                                    onChange={(_e, d) => setGridSearch(d.value)}
                                    style={{ minWidth: '300px' }}
                                />
                            </div>

                            <Divider />

                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHeaderCell style={{ width: '40px' }}>#</TableHeaderCell>
                                        <TableHeaderCell 
                                            style={{ cursor: 'pointer' }} 
                                            onClick={() => handleSort(selectedTab === 'seccion' ? 'code' : 'sectionCode')}
                                        >
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                Código
                                                {sortConfig.key === (selectedTab === 'seccion' ? 'code' : 'sectionCode') && (
                                                    sortConfig.direction === 'ascending' ? <TriangleUpRegular /> : <TriangleDownRegular />
                                                )}
                                            </div>
                                        </TableHeaderCell>
                                        <TableHeaderCell 
                                            style={{ cursor: 'pointer' }} 
                                            onClick={() => handleSort(selectedTab === 'seccion' ? 'name' : 'courseName')}
                                        >
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                {selectedTab === 'seccion' ? 'Nombre' : 'Curso / Sección'}
                                                {sortConfig.key === (selectedTab === 'seccion' ? 'name' : 'courseName') && (
                                                    sortConfig.direction === 'ascending' ? <TriangleUpRegular /> : <TriangleDownRegular />
                                                )}
                                            </div>
                                        </TableHeaderCell>
                                        <TableHeaderCell 
                                            style={{ cursor: 'pointer' }} 
                                            onClick={() => handleSort(selectedTab === 'seccion' ? 'status' : 'studentStatus')}
                                        >
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                                Estado
                                                {sortConfig.key === (selectedTab === 'seccion' ? 'status' : 'studentStatus') && (
                                                    sortConfig.direction === 'ascending' ? <TriangleUpRegular /> : <TriangleDownRegular />
                                                )}
                                            </div>
                                        </TableHeaderCell>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {processedData.length > 0 ? (
                                        processedData.map((item: any, index) => (
                                            <TableRow key={index}>
                                                <TableCell>{index + 1}</TableCell>
                                                <TableCell style={{ fontFamily: 'monospace' }}>
                                                    {selectedTab === 'seccion' ? item.code : item.sectionCode}
                                                </TableCell>
                                                <TableCell>
                                                    {selectedTab === 'seccion' ? (
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                            <Avatar name={item.name} size={24} color="colorful" />
                                                            <span style={{ fontWeight: 600 }}>{item.name || 'MISSING'}</span>
                                                        </div>
                                                    ) : (
                                                        <span style={{ fontWeight: 600 }}>{item.courseName}</span>
                                                    )}
                                                </TableCell>
                                                <TableCell>
                                                    {selectedTab === 'seccion' ? (
                                                        <Badge
                                                            appearance="filled"
                                                            color={
                                                                (item.status === 'En Team') ? 'success' :
                                                                    (item.status === 'Pendiente') ? 'warning' :
                                                                        (item.status === 'Sin Team') ? 'danger' :
                                                                            'brand'
                                                            }
                                                            className={styles.statusBadge}
                                                        >
                                                            {item.status}
                                                        </Badge>
                                                    ) : (
                                                        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                                            <Badge appearance="outline" color={item.teamStatus === 'Activo' ? 'success' : 'important'}>
                                                                Team: {item.teamStatus}
                                                            </Badge>
                                                            <Badge
                                                                appearance="filled"
                                                                color={
                                                                    item.studentStatus === 'En Team' ? 'success' :
                                                                        item.studentStatus === 'Pendiente' ? 'warning' : 'danger'
                                                                }
                                                            >
                                                                {item.studentStatus}
                                                            </Badge>
                                                        </div>
                                                    )}
                                                </TableCell>
                                            </TableRow>
                                        ))
                                    ) : (
                                        <TableRow>
                                            <TableCell colSpan={4} style={{ textAlign: 'center', padding: '40px', color: tokens.colorNeutralForeground3 }}>
                                                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '12px' }}>
                                                    {(selectedTab === 'seccion' ? !seccionData : !alumnoData) ? (
                                                        <>
                                                            <GridDotsRegular fontSize={48} />
                                                            No hay datos cargados. Por favor busca una {(selectedTab === 'seccion' ? 'sección' : 'alumno')}.
                                                        </>
                                                    ) : (
                                                        <>
                                                            <SearchRegular fontSize={48} />
                                                            No se encontraron resultados que coincidan con la búsqueda.
                                                        </>
                                                    )}
                                                </div>
                                            </TableCell>
                                        </TableRow>
                                    )}
                                </TableBody>
                            </Table>
                        </>
                    )}
                </div>
            </div>
        </div>
    );
};

const DetailItem: React.FC<{ label: string; value?: string | null }> = ({ label, value }) => {
    const styles = useStyles();
    if (!value || value === 'N/A') return null;

    return (
        <div className={styles.detailItem}>
            <span className={styles.detailLabel}>{label}</span>
            <span className={styles.detailValue}>{value}</span>
        </div>
    );
}

const CopyDetailItem: React.FC<{
    label: string;
    value?: string | null;
    sharePointUrl?: string | null;
    onCopy: (value?: string | null, source?: 'class' | 'sharepoint') => void;
    hideSharePoint?: boolean;
}> = ({ label, value, sharePointUrl, onCopy, hideSharePoint }) => {
    const styles = useStyles();
    if (!value || value === 'N/A') return null;

    return (
        <div className={styles.detailItem}>
            <span className={styles.detailLabel}>{label}</span>
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                <Button
                    appearance="primary"
                    icon={<CopyRegular />}
                    onClick={() => void onCopy(value, 'class')}
                    style={{ width: 'fit-content' }}
                >
                    Copiar link
                </Button>
                {!hideSharePoint && (
                    <Button
                        appearance="secondary"
                        icon={<ArrowUploadRegular />}
                        onClick={() => void onCopy(sharePointUrl, 'sharepoint')}
                        disabled={!sharePointUrl}
                        style={{ width: 'fit-content' }}
                    >
                        Ir a SharePoint
                    </Button>
                )}
            </div>
        </div>
    );
}


const formatDateTime = (value?: string | null) => {
    if (!value) return '';
    const date = new Date(forceUtcDate(value));
    if (Number.isNaN(date.getTime())) {
        return value;
    }
    return new Intl.DateTimeFormat('es-PE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        timeZone: 'America/Lima'
    }).format(date);
};

const formatTimeOnly = (value?: string | null) => {
    if (!value) return '';
    const date = new Date(forceUtcDate(value));
    if (Number.isNaN(date.getTime())) {
        return value;
    }
    return new Intl.DateTimeFormat('es-PE', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        timeZone: 'America/Lima'
    }).format(date);
};


const formatDuration = (seconds?: number | null) => {
    if (seconds === undefined || seconds === null) return '0s';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    if (h > 0) return `${h}h ${m}m ${s}s`;
    if (m > 0) return `${m}m ${s}s`;
    return `${s}s`;
};

export default OperationsPage;
