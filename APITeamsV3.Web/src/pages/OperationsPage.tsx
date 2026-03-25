import type { SelectTabData, TabValue } from '@fluentui/react-components';
import React, { useState } from 'react';
import {
    TabList,
    Tab,
    makeStyles,
    shorthands,
    Button,
    Input,
    Label,
    Checkbox,
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
} from '@fluentui/react-components';
import {
    SearchRegular,
    CheckmarkRegular,
    AddRegular,
    SaveRegular,
    CalendarRegular,
    ArrowUploadRegular,
    GridDotsRegular,
} from '@fluentui/react-icons';
import { useApiClient } from '../hooks/useApiClient';

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
    semestre: string;
    profesor?: string; // Optional/Nullable in API
    unidadNegocio: string;
    hasTeam: boolean;
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
});

const OperationsPage: React.FC = () => {
    const styles = useStyles();
    const apiClient = useApiClient();

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    // State for Top Tabs
    const [selectedTab, setSelectedTab] = useState<TabValue>('seccion');

    // State for Bottom Tabs
    const [selectedBottomTab, setSelectedBottomTab] = useState<TabValue>('reporte');

    // Form State (Seccion)
    const [seccionCodigo, setSeccionCodigo] = useState('');
    const [seccionData, setSeccionData] = useState<SectionData | null>(null);
    const [ownerEmail, setOwnerEmail] = useState('admin@idat.edu.pe');

    // Form State (Alumno)
    const [alumnoCodigo, setAlumnoCodigo] = useState('');
    const [alumnoData, setAlumnoData] = useState<StudentData | null>(null);

    const handleSearchSeccion = async () => {
        if (!seccionCodigo) return;
        setLoading(true);
        setError('');
        setSeccionData(null);
        try {
            const response = await apiClient.get(`/sections/search?code=${seccionCodigo}`);
            setSeccionData(response.data);
            if (!response.data.esTeams) {
                setError('Esta sección no está marcada para Teams (EsTeams = 0). Procede con precaución.');
            }
        } catch (err: any) {
            setError(err.message || 'Error al buscar sección');
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
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error desconocido al buscar alumno';
            setError(errorMessage);
        } finally {
            setLoading(false);
        }
    };

    const getCompanyKey = () => window.location.hostname.includes('zegel') ? 'zegel' : 'idat';

    const handleProvisionTeam = async () => {
        if (!seccionData?.idSeccion) return;
        if (!confirm('¿Estás seguro de crear/sincronizar el equipo para esta sección?')) return;

        setLoading(true);
        try {
            const companyKey = getCompanyKey();
            const response = await apiClient.post(`/sync/section/${seccionData.idSeccion}?companyKey=${companyKey}`);
            const result = response.data;
            alert(`Sincronización finalizada exitosamente. Equipos creados/actualizados: ${result.success}`);
            await handleSearchSeccion();
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : 'Error desconocido al iniciar sincronización';
            alert(errorMessage);
        } finally {
            setLoading(false);
        }
    };

    const handleVerifyTeam = async () => {
        if (!seccionData?.idSeccion) return;
        setLoading(true);
        try {
            const response = await apiClient.post(`/sync/verify/${seccionData.idSeccion}`);
            const result = response.data;
            alert(`Verificación: ${result.isValid ? 'OK' : 'Inconsistencias halladas'}\n\nDetalle: ${result.summary}`);
            await handleSearchSeccion();
        } catch (err: unknown) {
            alert('Error al verificar equipo.');
        } finally {
            setLoading(false);
        }
    };

    const handleRegenerateAgenda = async () => {
        if (!seccionData?.idSeccion) return;
        if (!confirm('Esto invalidará las reuniones pasadas y creará una nueva reunión de canal. ¿Proceder?')) return;
        setLoading(true);
        try {
            const companyKey = getCompanyKey();
            const response = await apiClient.post(`/sync/agenda/regenerate/${seccionData.idSeccion}?companyKey=${companyKey}`);
            alert(response.data.summary);
        } catch (err: unknown) {
            alert('Error al regenerar agenda.');
        } finally {
            setLoading(false);
        }
    };

    const handleSyncAlumno = async () => {
        if (!alumnoData?.codigo) return;
        setLoading(true);
        try {
            const response = await apiClient.post(`/sync/student/${alumnoData.codigo}`);
            alert(`Sincronización de alumno encolada. ${response.data.message || ''}`);
        } catch (err: unknown) {
            alert('Error al sincronizar alumno.');
        } finally {
            setLoading(false);
        }
    };



    const onTabSelect = (_: unknown, data: SelectTabData) => {
        if (data?.value) {
            setSelectedTab(data.value);
        }
    };

    const onBottomTabSelect = (_: unknown, data: SelectTabData) => {
        if (data?.value) {
            setSelectedBottomTab(data.value);
        }
    };

    return (
        <div className={styles.root}>
            <div className={styles.header}>
                <div className={styles.headerTitle}>
                    <Avatar color="brand" icon={<GridDotsRegular />} size={48} />
                    <div>
                        <Title3>Operations Dashboard</Title3>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground2 }}>Teams Provisioning Management</div>
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '10px' }}>
                    <Button icon={<ArrowUploadRegular />} >Exportar Reporte</Button>
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
                                Verificar Estado
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
                                <DetailItem label="Semestre" value={seccionData.semestre} />
                                <DetailItem label="Facilitador" value={seccionData.profesor || 'N/A'} />
                                <DetailItem label="Unidad Negocio" value={seccionData.unidadNegocio} />
                            </div>
                        )}

                        {seccionData && (
                            <div className={styles.actionsRegion}>
                                {seccionData.hasTeam ? (
                                    <>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: tokens.colorPaletteGreenForeground1 }}>
                                            <CheckmarkRegular fontSize={24} />
                                            <Title3>Equipo Activo</Title3>
                                        </div>
                                        <span style={{ flex: 1 }}></span>
                                        <Button icon={<SaveRegular />} onClick={handleProvisionTeam} disabled={loading}>Refrescar Miembros</Button>
                                        <Button icon={<CalendarRegular />} onClick={handleRegenerateAgenda} disabled={loading}>Regenerar Agendas</Button>
                                    </>
                                ) : (
                                    <>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: tokens.colorPaletteRedForeground1 }}>
                                            <Title3>No existe Equipo</Title3>
                                        </div>
                                        <span style={{ flex: 1 }}></span>
                                        <div style={{ display: 'flex', gap: '8px', alignItems: 'end' }}>
                                            <div className={styles.inputGroup} style={{ maxWidth: '250px' }}>
                                                <Label size="small">Owner Email</Label>
                                                <Input
                                                    value={ownerEmail}
                                                    onChange={(_e, d) => setOwnerEmail(d.value)}
                                                    type="email"
                                                    placeholder="admin@idat.edu.pe"
                                                />
                                            </div>
                                            <Button
                                                appearance="primary"
                                                style={{ backgroundColor: tokens.colorPaletteBlueBackground2 }}
                                                onClick={handleProvisionTeam}
                                                disabled={loading || !ownerEmail}
                                            >
                                                Crear Equipo en Teams
                                            </Button>
                                        </div>
                                    </>
                                )}
                                <Button icon={<AddRegular />} onClick={() => setSeccionData(null)}>Limpiar</Button>
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
                            <Checkbox label="Generar Agendas" size="large" />
                            <Checkbox label="Agregar Miembros" size="large" />
                            <span style={{ flex: 1 }}></span>
                            <Button icon={<AddRegular />} onClick={() => setAlumnoData(null)}>Limpiar</Button>
                            <Button icon={<SaveRegular />} appearance="primary" onClick={handleSyncAlumno} disabled={!alumnoData || loading}>Actualizar Teams</Button>
                        </div>

                    </div>
                )}
            </div>

            {/* Bottom Data Grids */}
            <div className={styles.bottomTabs}>
                <Title3>Resultados y Detalles</Title3>
                <div className={styles.tableContainer}>
                    <div style={{ padding: '16px' }}>
                        <TabList selectedValue={selectedBottomTab} onTabSelect={onBottomTabSelect}>
                            <Tab value="reporte">
                                {selectedTab === 'seccion' ? `Listado de Alumnos (${seccionData?.members?.length || 0})` : 'Cursos Matriculados'}
                            </Tab>
                            <Tab value="smart">Detalle Smart</Tab>
                            <Tab value="teams">Detalle Teams</Tab>
                        </TabList>
                    </div>

                    <Divider />

                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>#</TableHeaderCell>
                                <TableHeaderCell>Código</TableHeaderCell>
                                <TableHeaderCell>
                                    {selectedTab === 'seccion' ? 'Nombre' : 'Curso / Sección'}
                                </TableHeaderCell>
                                <TableHeaderCell>Estado</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {selectedTab === 'seccion' && seccionData?.members ? (
                                seccionData.members.map((member, index) => (
                                    <TableRow key={index}>
                                        <TableCell>{index + 1}</TableCell>
                                        <TableCell style={{ fontFamily: 'monospace' }}>
                                            {member.code || 'MISSING'}
                                        </TableCell>
                                        <TableCell>
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                <Avatar name={member.name} size={24} color="colorful" />
                                                <span style={{ fontWeight: 600 }}>{member.name || 'MISSING'}</span>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <Badge
                                                appearance="filled"
                                                color={
                                                    (member.status === 'En Team') ? 'success' :
                                                        (member.status === 'Pendiente') ? 'warning' :
                                                            (member.status === 'Sin Team') ? 'danger' :
                                                                'brand'
                                                }
                                                className={styles.statusBadge}
                                            >
                                                {member.status}
                                            </Badge>
                                        </TableCell>
                                    </TableRow>
                                ))
                            ) : selectedTab === 'alumno' && alumnoData?.enrolledSections ? (
                                alumnoData.enrolledSections.map((section, index) => (
                                    <TableRow key={index}>
                                        <TableCell>{index + 1}</TableCell>
                                        <TableCell style={{ fontFamily: 'monospace' }}>
                                            {section.sectionCode}
                                        </TableCell>
                                        <TableCell>
                                            <span style={{ fontWeight: 600 }}>{section.courseName}</span>
                                        </TableCell>
                                        <TableCell>
                                            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                                <Badge appearance="outline" color={section.teamStatus === 'Activo' ? 'success' : 'important'}>
                                                    Team: {section.teamStatus}
                                                </Badge>
                                                <Badge
                                                    appearance="filled"
                                                    color={
                                                        section.studentStatus === 'En Team' ? 'success' :
                                                            section.studentStatus === 'Pendiente' ? 'warning' : 'danger'
                                                    }
                                                >
                                                    {section.studentStatus}
                                                </Badge>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))
                            ) : (
                                <TableRow>
                                    <TableCell colSpan={selectedTab === 'seccion' ? 4 : 3} style={{ textAlign: 'center', padding: '20px', color: tokens.colorNeutralForeground3 }}>
                                        No hay datos para mostrar. Realice una búsqueda.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </div>
            </div>
        </div>
    );
};

const DetailItem: React.FC<{ label: string; value: string }> = ({ label, value }) => {
    const styles = useStyles();
    if (!value || value === 'N/A') return null;

    return (
        <div className={styles.detailItem}>
            <span className={styles.detailLabel}>{label}</span>
            <span className={styles.detailValue}>{value}</span>
        </div>
    );
}

export default OperationsPage;
