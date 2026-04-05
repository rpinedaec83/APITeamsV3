import React, { useEffect, useState } from 'react';
import { useApiClient } from '../hooks/useApiClient';
import { useMsal } from "@azure/msal-react";
import {
    Badge,
    Button,
    Card,
    Input,
    Select,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    Title1,
    Title3,
    makeStyles,
    shorthands,
    tokens,
} from '@fluentui/react-components';
import {
    ArrowTrendingRegular,
    BoardRegular,
    BranchCompareRegular,
    GroupRegular,
    HatGraduationRegular,
    PeopleRegular,
    PersonStarRegular,
    PulseRegular,
    WarningRegular,
} from '@fluentui/react-icons';
import { useNavigate } from 'react-router-dom';

const useStyles = makeStyles({
    root: {
        minHeight: '100%',
        padding: '28px',
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        background: 'radial-gradient(circle at 10% 20%, rgba(243, 248, 253, 1) 0%, rgba(228, 237, 248, 1) 90%)',
    },
    hero: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1.8fr) minmax(320px, 1fr)',
        gap: '18px',
        '@media (max-width: 1024px)': {
            gridTemplateColumns: '1fr',
        },
    },
    heroPanel: {
        color: '#fff',
        background: 'linear-gradient(135deg, rgba(31, 45, 61, 0.95) 0%, rgba(44, 62, 80, 0.9) 55%, rgba(57, 81, 107, 0.85) 100%)',
        backdropFilter: 'blur(12px)',
        ...shorthands.borderRadius('24px'),
        ...shorthands.padding('32px'),
        boxShadow: '0 24px 48px rgba(31, 45, 61, 0.3)',
        position: 'relative',
        overflow: 'hidden',
        border: '1px solid rgba(255, 255, 255, 0.08)',
    },
    heroGlow: {
        position: 'absolute',
        insetInlineEnd: '-60px',
        insetBlockStart: '-60px',
        width: '320px',
        height: '320px',
        borderRadius: '999px',
        background: 'radial-gradient(circle, rgba(0,192,239,0.35) 0%, rgba(0,192,239,0.15) 45%, rgba(0,192,239,0) 75%)',
        filter: 'blur(40px)',
    },
    heroGlowSecondary: {
        position: 'absolute',
        insetInlineStart: '-40px',
        insetBlockEnd: '-40px',
        width: '200px',
        height: '200px',
        borderRadius: '999px',
        background: 'radial-gradient(circle, rgba(142, 68, 173, 0.2) 0%, rgba(142, 68, 173, 0) 70%)',
        filter: 'blur(30px)',
    },
    heroContent: {
        position: 'relative',
        zIndex: 1,
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
    heroMeta: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '10px',
    },
    heroMetricRow: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
        gap: '12px',
        '@media (max-width: 768px)': {
            gridTemplateColumns: '1fr',
        },
    },
    heroMetric: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
        backgroundColor: 'rgba(255,255,255,0.1)',
        ...shorthands.borderRadius('16px'),
        ...shorthands.padding('20px'),
        border: '1px solid rgba(255,255,255,0.08)',
        transition: 'all 0.25s cubic-bezier(0.4, 0, 0.2, 1)',
        cursor: 'default',
        '&:hover': {
            backgroundColor: 'rgba(255,255,255,0.18)',
            transform: 'translateY(-4px)',
            ...shorthands.borderColor('rgba(255,255,255,0.2)'),
            boxShadow: '0 8px 16px rgba(0,0,0,0.2)',
        }
    },
    sidePanel: {
        backgroundColor: '#ffffff',
        ...shorthands.borderRadius('18px'),
        ...shorthands.padding('22px'),
        boxShadow: tokens.shadow8,
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
    },
    smallBoxGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
        gap: '16px',
        '@media (max-width: 1200px)': {
            gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        },
        '@media (max-width: 640px)': {
            gridTemplateColumns: '1fr',
        },
    },
    smallBox: {
        position: 'relative',
        minHeight: '172px',
        color: '#fff',
        ...shorthands.borderRadius('20px'),
        ...shorthands.padding('24px'),
        boxShadow: '0 8px 16px rgba(44, 62, 80, 0.12)',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        transition: 'all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1)',
        border: '1px solid rgba(255,255,255,0.12)',
        '&:hover': {
            transform: 'translateY(-6px)',
            boxShadow: '0 20px 40px rgba(44, 62, 80, 0.2)',
        },
    },
    smallBoxIcon: {
        position: 'absolute',
        insetInlineEnd: '20px',
        insetBlockStart: '20px',
        opacity: 0.15,
        transform: 'scale(1.9)',
        transition: 'all 0.4s ease',
    },
    smallBoxContent: {
        position: 'relative',
        zIndex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: '10px',
        maxWidth: '72%',
    },
    smallBoxValue: {
        fontSize: '3rem',
        lineHeight: 1,
        fontWeight: 800,
        letterSpacing: '-0.04em',
    },
    smallBoxLabel: {
        fontSize: tokens.fontSizeBase400,
        lineHeight: 1.25,
        fontWeight: tokens.fontWeightSemibold,
        color: 'rgba(255,255,255,0.96)',
    },
    smallBoxMeta: {
        fontSize: tokens.fontSizeBase200,
        lineHeight: 1.4,
        color: 'rgba(255,255,255,0.84)',
    },
    smallBoxPercent: {
        display: 'inline-flex',
        alignItems: 'center',
        width: 'fit-content',
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        color: '#fff',
        backgroundColor: 'rgba(255,255,255,0.14)',
        ...shorthands.padding('4px', '10px'),
        ...shorthands.borderRadius('999px'),
        border: '1px solid rgba(255,255,255,0.16)',
    },
    panels: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1.25fr) minmax(0, 1fr)',
        gap: '18px',
        '@media (max-width: 1100px)': {
            gridTemplateColumns: '1fr',
        },
    },
    panelCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius('18px'),
        boxShadow: tokens.shadow8,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
    },
    panelHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '12px',
        ...shorthands.padding('18px', '20px'),
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: '#f7f9fc',
    },
    panelBody: {
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
        ...shorthands.padding('18px', '20px', '20px'),
    },
    progressGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    progressLabelRow: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: '12px',
        alignItems: 'center',
    },
    progressTrack: {
        width: '100%',
        height: '10px',
        backgroundColor: '#e6edf5',
        ...shorthands.borderRadius('999px'),
        overflow: 'hidden',
    },
    progressBar: {
        height: '100%',
        ...shorthands.borderRadius('999px'),
        boxShadow: '0 0 10px rgba(255,255,255,0.2)',
        position: 'relative',
        overflow: 'hidden',
        '&::after': {
            content: '""',
            position: 'absolute',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'linear-gradient(90deg, rgba(255,255,255,0) 0%, rgba(255,255,255,0.2) 50%, rgba(255,255,255,0) 100%)',
            animation: 'shimmer 2s infinite linear',
        }
    },
    '@keyframes shimmer': {
        '0%': { transform: 'translateX(-100%)' },
        '100%': { transform: 'translateX(100%)' },
    },
    insightList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    insightItem: {
        display: 'grid',
        gridTemplateColumns: 'minmax(0, 1fr) auto',
        gap: '10px',
        alignItems: 'center',
        ...shorthands.padding('12px', '14px'),
        backgroundColor: '#f8fafc',
        ...shorthands.borderRadius('12px'),
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    tableWrap: {
        overflowX: 'auto',
    },
    filterBar: {
        display: 'grid',
        gridTemplateColumns: 'minmax(220px, 1.4fr) repeat(5, minmax(140px, 1fr))',
        gap: '12px',
        alignItems: 'end',
        '@media (max-width: 1200px)': {
            gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
        },
        '@media (max-width: 700px)': {
            gridTemplateColumns: '1fr',
        },
    },
    filterField: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
    },
    badgeRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '8px',
    },
    emptyState: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius('18px'),
        ...shorthands.padding('28px'),
        textAlign: 'center',
        boxShadow: tokens.shadow4,
    },
});

interface TenancyStatsRow {
    idPeriodo: number;
    sede: string;
    periodo: string;
    unidadNegocio: string;
    programa: string;
    equipos: number;
    equiposActivos: number;
    porEquiposActivos: number;
    docentes: number;
    noDocentes: number;
    porDocente: number;
    cursoxAlumnos: number;
    cursoxTeams: number;
    porCursoxAlumnos: number;
    alumnos: number;
    enTeams: number;
    porAlumnos: number;
}

interface DashboardSummary {
    companyKey: string;
    displayName: string;
    isPilotMode: boolean;
    pilotSectionsConfigured: number;
    defaultChannelName: string;
    meetingPolicyMode: string;
    timeZoneId: string;
    rows: TenancyStatsRow[];
}

interface AggregateStats {
    equipos: number;
    equiposActivos: number;
    alumnos: number;
    enTeams: number;
    docentes: number;
    noDocentes: number;
    cursoxAlumnos: number;
    cursoxTeams: number;
}

const smallBoxColors = {
    teal: 'linear-gradient(135deg, #02b3df 0%, #0089a8 100%)',
    green: 'linear-gradient(135deg, #27ae60 0%, #1e8449 100%)',
    yellow: 'linear-gradient(135deg, #f39c12 0%, #e67e22 100%)',
    red: 'linear-gradient(135deg, #e74c3c 0%, #c0392b 100%)',
};

const Dashboard: React.FC = () => {
    const { accounts } = useMsal();
    const api = useApiClient();
    const styles = useStyles();
    const navigate = useNavigate();
    const [summary, setSummary] = useState<DashboardSummary | null>(null);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [selectedPeriodo, setSelectedPeriodo] = useState('all');
    const [selectedSede, setSelectedSede] = useState('all');
    const [selectedUnidad, setSelectedUnidad] = useState('all');
    const [selectedPrograma, setSelectedPrograma] = useState('all');
    const [selectedCoverage, setSelectedCoverage] = useState('all');

    useEffect(() => {
        api.get('/reports/dashboard-summary')
            .then(res => setSummary(res.data as DashboardSummary))
            .catch(err => console.error(err))
            .finally(() => setLoading(false));
    }, [api]);

    const rows = summary?.rows ?? [];
    const aggregate = rows.reduce<AggregateStats>((acc, curr) => ({
        equipos: acc.equipos + curr.equipos,
        equiposActivos: acc.equiposActivos + curr.equiposActivos,
        alumnos: acc.alumnos + curr.alumnos,
        enTeams: acc.enTeams + curr.enTeams,
        docentes: acc.docentes + curr.docentes,
        noDocentes: acc.noDocentes + curr.noDocentes,
        cursoxAlumnos: acc.cursoxAlumnos + curr.cursoxAlumnos,
        cursoxTeams: acc.cursoxTeams + curr.cursoxTeams,
    }), {
        equipos: 0,
        equiposActivos: 0,
        alumnos: 0,
        enTeams: 0,
        docentes: 0,
        noDocentes: 0,
        cursoxAlumnos: 0,
        cursoxTeams: 0,
    });

    const teamCoverage = aggregate.equipos > 0 ? (aggregate.equiposActivos / aggregate.equipos) * 100 : 0;
    const studentCoverage = aggregate.alumnos > 0 ? (aggregate.enTeams / aggregate.alumnos) * 100 : 0;
    const courseCoverage = aggregate.cursoxAlumnos > 0 ? (aggregate.cursoxTeams / aggregate.cursoxAlumnos) * 100 : 0;
    const teacherCoverage = aggregate.equipos > 0 ? (aggregate.docentes / aggregate.equipos) * 100 : 0;
    const programsCount = new Set(rows.map(row => `${row.unidadNegocio}|${row.programa}`)).size;
    const sedeCount = new Set(rows.map(row => row.sede)).size;
    const periodCount = new Set(rows.map(row => row.idPeriodo)).size;

    const topSedes = [...rows]
        .sort((a, b) => b.equiposActivos - a.equiposActivos || b.enTeams - a.enTeams)
        .slice(0, 4);

    const lowCoverageRows = [...rows]
        .sort((a, b) => Number(a.porAlumnos) - Number(b.porAlumnos))
        .slice(0, 5);

    const numberFormatter = new Intl.NumberFormat('es-PE');
    const percent = (value: number) => `${Math.round(value)}%`;
    const unidadOptions = Array.from(new Set(rows.map(row => row.unidadNegocio))).sort();
    const programaOptions = Array.from(new Set(
        rows
            .filter(row => selectedUnidad === 'all' || row.unidadNegocio === selectedUnidad)
            .map(row => row.programa)
    )).sort();
    const periodoOptions = Array.from(new Set(
        rows
            .filter(row =>
                (selectedUnidad === 'all' || row.unidadNegocio === selectedUnidad) &&
                (selectedPrograma === 'all' || row.programa === selectedPrograma))
            .map(row => row.periodo)
    )).sort();
    const sedeOptions = Array.from(new Set(
        rows
            .filter(row =>
                (selectedUnidad === 'all' || row.unidadNegocio === selectedUnidad) &&
                (selectedPrograma === 'all' || row.programa === selectedPrograma) &&
                (selectedPeriodo === 'all' || row.periodo === selectedPeriodo))
            .map(row => row.sede)
    )).sort();

    const filteredRows = rows.filter(row => {
        const coveragePercent = Number(row.porAlumnos) * 100;
        const coverageBucket =
            coveragePercent >= 80 ? 'high' :
            coveragePercent >= 50 ? 'medium' :
            'low';

        const text = `${row.periodo} ${row.sede} ${row.unidadNegocio} ${row.programa}`.toLowerCase();
        const normalizedSearch = searchTerm.trim().toLowerCase();

        return (selectedPeriodo === 'all' || row.periodo === selectedPeriodo) &&
            (selectedSede === 'all' || row.sede === selectedSede) &&
            (selectedUnidad === 'all' || row.unidadNegocio === selectedUnidad) &&
            (selectedPrograma === 'all' || row.programa === selectedPrograma) &&
            (selectedCoverage === 'all' || coverageBucket === selectedCoverage) &&
            (!normalizedSearch || text.includes(normalizedSearch));
    });

    const renderProgress = (label: string, value: number, color: string, meta: string) => (
        <div className={styles.progressGroup}>
            <div className={styles.progressLabelRow}>
                <Text weight="semibold">{label}</Text>
                <Text>{meta}</Text>
            </div>
            <div className={styles.progressTrack}>
                <div className={styles.progressBar} style={{ width: `${Math.max(0, Math.min(100, value))}%`, background: color }} />
            </div>
        </div>
    );

    return (
        <div className={styles.root}>
            <div className={styles.hero}>
                <div className={styles.heroPanel}>
                    <div className={styles.heroGlow} />
                    <div className={styles.heroGlowSecondary} />
                    <div className={styles.heroContent} style={{ gap: '8px' }}>
                        <div className={styles.eyebrow}>Resumen Operativo</div>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                            <Title1 style={{ color: '#fff', margin: 0 }}>Dashboard Ejecutivo</Title1>
                            <Text style={{ color: 'rgba(255,255,255,0.78)' }}>
                                {summary?.displayName ?? 'Tenant actual'} · Bienvenido, {accounts[0]?.name}
                            </Text>
                        </div>
                        <div className={styles.heroMeta}>
                            <Badge appearance="filled" color={summary?.isPilotMode ? 'warning' : 'success'}>
                                {summary?.isPilotMode ? 'Pilot Mode activo' : 'Despliegue total'}
                            </Badge>
                            {summary?.defaultChannelName ? (
                                <Badge appearance="filled" color="informative">Canal default: {summary.defaultChannelName}</Badge>
                            ) : null}
                            {summary?.meetingPolicyMode ? (
                                <Badge appearance="filled" color="brand">Agenda: {summary.meetingPolicyMode}</Badge>
                            ) : null}
                            {summary?.timeZoneId ? (
                                <Badge appearance="filled">TZ: {summary.timeZoneId}</Badge>
                            ) : null}
                        </div>
                        <div className={styles.heroMetricRow}>
                            <div className={styles.heroMetric}>
                                <Text size={800} weight="bold">{numberFormatter.format(programsCount)}</Text>
                                <Text size={200} style={{ color: 'rgba(255,255,255,0.8)' }}>Programas monitoreados</Text>
                            </div>
                            <div className={styles.heroMetric}>
                                <Text size={800} weight="bold">{numberFormatter.format(sedeCount)}</Text>
                                <Text size={200} style={{ color: 'rgba(255,255,255,0.8)' }}>Sedes activas en tablero</Text>
                            </div>
                            <div className={styles.heroMetric}>
                                <Text size={800} weight="bold">{numberFormatter.format(periodCount)}</Text>
                                <Text size={200} style={{ color: 'rgba(255,255,255,0.8)' }}>Periodos activos</Text>
                            </div>
                        </div>
                    </div>
                </div>

                <div className={styles.sidePanel}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', gap: '12px' }}>
                        <div>
                            <Text weight="semibold" size={500}>Estado del tenant</Text>
                            <div className={styles.badgeRow} style={{ marginTop: '10px' }}>
                                <Badge color={summary?.isPilotMode ? 'warning' : 'success'} appearance="filled">
                                    {summary?.isPilotMode ? 'Solo secciones piloto' : 'Cobertura global'}
                                </Badge>
                            </div>
                        </div>
                        <Button appearance="primary" icon={<ArrowTrendingRegular />} onClick={() => navigate('/operations')}>
                            Ir a Operaciones
                        </Button>
                    </div>

                    <Card>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                                <Text size={800} weight="bold">{numberFormatter.format(summary?.pilotSectionsConfigured ?? 0)}</Text>
                                <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>Secciones en whitelist piloto</Text>
                            </div>
                            <WarningRegular fontSize={28} color={summary?.isPilotMode ? '#f39c12' : '#00a65a'} />
                        </div>
                    </Card>

                    {renderProgress('Cobertura de Teams', teamCoverage, 'linear-gradient(90deg, #00c0ef 0%, #0097bc 100%)', `${numberFormatter.format(aggregate.equiposActivos)} / ${numberFormatter.format(aggregate.equipos)}`)}
                    {renderProgress('Cobertura de estudiantes', studentCoverage, 'linear-gradient(90deg, #00a65a 0%, #008d4c 100%)', `${numberFormatter.format(aggregate.enTeams)} / ${numberFormatter.format(aggregate.alumnos)}`)}
                    {renderProgress('Cobertura de membresías por curso', courseCoverage, 'linear-gradient(90deg, #f39c12 0%, #d68910 100%)', `${numberFormatter.format(aggregate.cursoxTeams)} / ${numberFormatter.format(aggregate.cursoxAlumnos)}`)}
                </div>
            </div>

            {loading ? (
                <div className={styles.emptyState}>
                    <Text size={500}>Cargando estadisticas operativas...</Text>
                </div>
            ) : !summary || rows.length === 0 ? (
                <div className={styles.emptyState}>
                    <Title3>No se encontraron datos para los periodos activos.</Title3>
                </div>
            ) : (
                <>
                    <div className={styles.smallBoxGrid}>
                        <div className={styles.smallBox} style={{ background: smallBoxColors.teal }}>
                            <div className={styles.smallBoxIcon}><BoardRegular fontSize={40} /></div>
                            <div className={styles.smallBoxContent}>
                                <div className={styles.smallBoxValue}>{numberFormatter.format(aggregate.equipos)}</div>
                                <div className={styles.smallBoxLabel}>Total de secciones monitoreadas</div>
                                <div className={styles.smallBoxPercent}>{percent(teamCoverage)}</div>
                                <div className={styles.smallBoxMeta}>Cobertura activa: {percent(teamCoverage)}</div>
                            </div>
                        </div>
                        <div className={styles.smallBox} style={{ background: smallBoxColors.green }}>
                            <div className={styles.smallBoxIcon}><GroupRegular fontSize={40} /></div>
                            <div className={styles.smallBoxContent}>
                                <div className={styles.smallBoxValue}>{numberFormatter.format(aggregate.equiposActivos)}</div>
                                <div className={styles.smallBoxLabel}>Teams activos</div>
                                <div className={styles.smallBoxPercent}>{percent(teacherCoverage)}</div>
                                <div className={styles.smallBoxMeta}>Con docente asignado: {percent(teacherCoverage)}</div>
                            </div>
                        </div>
                        <div className={styles.smallBox} style={{ background: smallBoxColors.yellow }}>
                            <div className={styles.smallBoxIcon}><PeopleRegular fontSize={40} /></div>
                            <div className={styles.smallBoxContent}>
                                <div className={styles.smallBoxValue}>{numberFormatter.format(aggregate.alumnos)}</div>
                                <div className={styles.smallBoxLabel}>Alumnos matriculados</div>
                                <div className={styles.smallBoxPercent}>{percent(studentCoverage)}</div>
                                <div className={styles.smallBoxMeta}>En Teams: {numberFormatter.format(aggregate.enTeams)}</div>
                            </div>
                        </div>
                        <div className={styles.smallBox} style={{ background: smallBoxColors.red }}>
                            <div className={styles.smallBoxIcon}><PersonStarRegular fontSize={40} /></div>
                            <div className={styles.smallBoxContent}>
                                <div className={styles.smallBoxValue}>{numberFormatter.format(aggregate.noDocentes)}</div>
                                <div className={styles.smallBoxLabel}>Teams sin docente mapeado</div>
                                <div className={styles.smallBoxPercent}>{percent(100 - teacherCoverage)}</div>
                                <div className={styles.smallBoxMeta}>Docentes detectados: {numberFormatter.format(aggregate.docentes)}</div>
                            </div>
                        </div>
                    </div>

                    <div className={styles.panels}>
                        <div className={styles.panelCard}>
                            <div className={styles.panelHeader}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Title3 style={{ margin: 0 }}>Cobertura Operativa</Title3>
                                    <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>Resumen de salud del tenant con foco en adopción.</Text>
                                </div>
                                <PulseRegular fontSize={24} color="#00a65a" />
                            </div>
                            <div className={styles.panelBody}>
                                {renderProgress('Teams provisionados', teamCoverage, 'linear-gradient(90deg, #00c0ef 0%, #3c8dbc 100%)', percent(teamCoverage))}
                                {renderProgress('Alumnos dentro de Teams', studentCoverage, 'linear-gradient(90deg, #00a65a 0%, #2ecc71 100%)', percent(studentCoverage))}
                                {renderProgress('Relacion curso/alumno sincronizada', courseCoverage, 'linear-gradient(90deg, #f39c12 0%, #f1c40f 100%)', percent(courseCoverage))}
                                {renderProgress('Secciones con docente', teacherCoverage, 'linear-gradient(90deg, #dd4b39 0%, #e74c3c 100%)', percent(teacherCoverage))}
                            </div>
                        </div>

                        <div className={styles.panelCard}>
                            <div className={styles.panelHeader}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Title3 style={{ margin: 0 }}>Focos de Atencion</Title3>
                                    <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>Sedes y programas con mejor y peor comportamiento.</Text>
                                </div>
                                <BranchCompareRegular fontSize={24} color="#f39c12" />
                            </div>
                            <div className={styles.panelBody}>
                                <div className={styles.insightList}>
                                    {topSedes.map((row, index) => (
                                        <div key={`${row.idPeriodo}-${row.sede}-${row.programa}-${index}`} className={styles.insightItem}>
                                            <div>
                                                <Text weight="semibold">{row.sede} · {row.programa}</Text>
                                                <div>
                                                    <Text size={200}>Teams activos: {numberFormatter.format(row.equiposActivos)} · Alumnos en Teams: {numberFormatter.format(row.enTeams)}</Text>
                                                </div>
                                            </div>
                                            <Badge appearance="filled" color="success">{percent(Number(row.porAlumnos) * 100)}</Badge>
                                        </div>
                                    ))}
                                </div>
                                <div style={{ borderTop: `1px solid ${tokens.colorNeutralStroke2}`, paddingTop: '10px' }}>
                                    <Text weight="semibold">Cobertura mas baja</Text>
                                </div>
                                <div className={styles.insightList}>
                                    {lowCoverageRows.map((row, index) => (
                                        <div key={`${row.idPeriodo}-${row.sede}-${row.programa}-low-${index}`} className={styles.insightItem}>
                                            <div>
                                                <Text weight="semibold">{row.sede} · {row.unidadNegocio}</Text>
                                                <div>
                                                    <Text size={200}>{row.programa} · Periodo {row.periodo}</Text>
                                                </div>
                                            </div>
                                            <Badge appearance="filled" color={Number(row.porAlumnos) < 0.5 ? 'danger' : 'warning'}>
                                                {percent(Number(row.porAlumnos) * 100)}
                                            </Badge>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className={styles.panelCard}>
                        <div className={styles.panelHeader}>
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                                    <Title3 style={{ margin: 0 }}>Detalle por Unidad, Programa y Periodo</Title3>
                                    <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>Vista tabular para seguimiento operativo fino.</Text>
                                </div>
                            <HatGraduationRegular fontSize={24} color="#3c8dbc" />
                        </div>
                        <div className={styles.panelBody}>
                            <div className={styles.filterBar}>
                                <div className={styles.filterField}>
                                    <Text size={200} weight="semibold">Búsqueda inteligente</Text>
                                    <Input
                                        value={searchTerm}
                                        onChange={(_, data) => setSearchTerm(data.value)}
                                        placeholder="Unidad, programa, periodo o sede"
                                    />
                                </div>
                                <div className={styles.filterField}>
                                    <Text size={200} weight="semibold">Unidad</Text>
                                    <Select value={selectedUnidad} onChange={(_, data) => {
                                        setSelectedUnidad(data.value);
                                        setSelectedPrograma('all');
                                        setSelectedPeriodo('all');
                                        setSelectedSede('all');
                                    }}>
                                        <option value="all">Todas</option>
                                        {unidadOptions.map(option => <option key={option} value={option}>{option}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterField}>
                                    <Text size={200} weight="semibold">Programa</Text>
                                    <Select value={selectedPrograma} onChange={(_, data) => {
                                        setSelectedPrograma(data.value);
                                        setSelectedPeriodo('all');
                                        setSelectedSede('all');
                                    }}>
                                        <option value="all">Todos</option>
                                        {programaOptions.map(option => <option key={option} value={option}>{option}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterField}>
                                    <Text size={200} weight="semibold">Periodo</Text>
                                    <Select value={selectedPeriodo} onChange={(_, data) => {
                                        setSelectedPeriodo(data.value);
                                        setSelectedSede('all');
                                    }}>
                                        <option value="all">Todos</option>
                                        {periodoOptions.map(option => <option key={option} value={option}>{option}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterField}>
                                    <Text size={200} weight="semibold">Sede</Text>
                                    <Select value={selectedSede} onChange={(_, data) => setSelectedSede(data.value)}>
                                        <option value="all">Todas</option>
                                        {sedeOptions.map(option => <option key={option} value={option}>{option}</option>)}
                                    </Select>
                                </div>
                                <div className={styles.filterField}>
                                    <Text size={200} weight="semibold">Cobertura</Text>
                                    <Select value={selectedCoverage} onChange={(_, data) => setSelectedCoverage(data.value)}>
                                        <option value="all">Todas</option>
                                        <option value="high">Alta</option>
                                        <option value="medium">Media</option>
                                        <option value="low">Crítica</option>
                                    </Select>
                                </div>
                            </div>
                            <div className={styles.badgeRow}>
                                <Badge appearance="outline">Filas visibles: {numberFormatter.format(filteredRows.length)}</Badge>
                                {selectedPeriodo !== 'all' ? <Badge appearance="outline" color="brand">Periodo: {selectedPeriodo}</Badge> : null}
                                {selectedSede !== 'all' ? <Badge appearance="outline" color="informative">Sede: {selectedSede}</Badge> : null}
                                {selectedCoverage !== 'all' ? <Badge appearance="outline" color="warning">Cobertura: {selectedCoverage}</Badge> : null}
                            </div>
                            <div className={styles.tableWrap}>
                                <Table aria-label="dashboard summary table">
                                    <TableHeader>
                                        <TableRow>
                                            <TableHeaderCell>Periodo</TableHeaderCell>
                                            <TableHeaderCell>Sede</TableHeaderCell>
                                            <TableHeaderCell>Unidad</TableHeaderCell>
                                            <TableHeaderCell>Programa</TableHeaderCell>
                                            <TableHeaderCell>Teams activos</TableHeaderCell>
                                            <TableHeaderCell>Docentes</TableHeaderCell>
                                            <TableHeaderCell>Alumnos</TableHeaderCell>
                                            <TableHeaderCell>En Teams</TableHeaderCell>
                                            <TableHeaderCell>% Alumnos</TableHeaderCell>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {filteredRows.map((row, index) => (
                                            <TableRow key={`${row.idPeriodo}-${row.sede}-${row.programa}-${index}`}>
                                                <TableCell>{row.periodo}</TableCell>
                                                <TableCell>{row.sede}</TableCell>
                                                <TableCell>{row.unidadNegocio}</TableCell>
                                                <TableCell>{row.programa}</TableCell>
                                                <TableCell>{numberFormatter.format(row.equiposActivos)} / {numberFormatter.format(row.equipos)}</TableCell>
                                                <TableCell>{numberFormatter.format(row.docentes)}</TableCell>
                                                <TableCell>{numberFormatter.format(row.alumnos)}</TableCell>
                                                <TableCell>{numberFormatter.format(row.enTeams)}</TableCell>
                                                <TableCell>
                                                    <Badge appearance="filled" color={Number(row.porAlumnos) >= 0.8 ? 'success' : Number(row.porAlumnos) >= 0.5 ? 'warning' : 'danger'}>
                                                        {percent(Number(row.porAlumnos) * 100)}
                                                    </Badge>
                                                </TableCell>
                                            </TableRow>
                                        ))}
                                        {filteredRows.length === 0 ? (
                                            <TableRow>
                                                <TableCell colSpan={9}>
                                                    <Text>No hay resultados con los filtros actuales.</Text>
                                                </TableCell>
                                            </TableRow>
                                        ) : null}
                                    </TableBody>
                                </Table>
                            </div>
                        </div>
                    </div>
                </>
            )}
        </div>
    );
};

export default Dashboard;
