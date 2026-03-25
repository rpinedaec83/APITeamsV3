import React, { useEffect, useState } from 'react';
import { useApiClient } from '../hooks/useApiClient';
import { useMsal } from "@azure/msal-react";
import { Title1, Title3, Card, CardHeader, CardPreview, Text, makeStyles, tokens, shorthands, Button } from '@fluentui/react-components';
import { ArrowTrendingRegular } from '@fluentui/react-icons';
import { useNavigate } from 'react-router-dom';

const useStyles = makeStyles({
    root: { padding: '32px', display: 'flex', flexDirection: 'column', gap: '24px' },
    grid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '20px' },
    card: { ...shorthands.padding('20px'), backgroundColor: tokens.colorNeutralBackground1, boxShadow: tokens.shadow4 }
});

interface TenancyStats {
    equipos: number;
    equiposActivos: number;
    alumnos: number;
    enTeams: number;
}

type TenancyStatsRow = TenancyStats;

const Dashboard: React.FC = () => {
    const { accounts } = useMsal();
    const api = useApiClient();
    const styles = useStyles();
    const navigate = useNavigate();
    const [stats, setStats] = useState<TenancyStats | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        api.get('/reports/tenancy-stats')
            .then(res => {
                const data = res.data as TenancyStatsRow[];
                if (data && data.length > 0) {
                    const aggregated = data.reduce<TenancyStats>((acc, curr) => ({
                        equipos: acc.equipos + curr.equipos,
                        equiposActivos: acc.equiposActivos + curr.equiposActivos,
                        alumnos: acc.alumnos + curr.alumnos,
                        enTeams: acc.enTeams + curr.enTeams
                    }), { equipos: 0, equiposActivos: 0, alumnos: 0, enTeams: 0 });
                    setStats(aggregated);
                } else {
                    setStats({ equipos: 0, equiposActivos: 0, alumnos: 0, enTeams: 0 });
                }
            })
            .catch(err => console.error(err))
            .finally(() => setLoading(false));
    }, [api]);

    const porTeams = stats && stats.equipos > 0 ? (stats.equiposActivos / stats.equipos) * 100 : 0;
    const porAlumnos = stats && stats.alumnos > 0 ? (stats.enTeams / stats.alumnos) * 100 : 0;

    return (
        <div className={styles.root}>
            <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center'}}>
                <div>
                    <Title1>Dashboard Principal</Title1>
                    <p>Bienvenido, {accounts[0]?.name}</p>
                </div>
                <Button appearance="primary" icon={<ArrowTrendingRegular />} onClick={() => navigate('/operations')}>Ir a Operaciones</Button>
            </div>

            {loading ? (
                <p>Cargando estadísticas...</p>
            ) : stats ? (
                <div className={styles.grid}>
                    <Card className={styles.card}>
                        <CardHeader header={<Title3>Total Secciones Activas</Title3>} />
                        <CardPreview>
                            <Text size={1000} weight="bold">{stats.equipos}</Text>
                            <p>Teams Creados: {stats.equiposActivos} ({Math.round(porTeams)}%)</p>
                        </CardPreview>
                    </Card>
                    <Card className={styles.card}>
                        <CardHeader header={<Title3>Alumnos Matriculados</Title3>} />
                        <CardPreview>
                            <Text size={1000} weight="bold">{stats.alumnos}</Text>
                            <p>Estudiantes en Teams: {stats.enTeams} ({Math.round(porAlumnos)}%)</p>
                        </CardPreview>
                    </Card>
                </div>
            ) : (
                <p>No se encontraron datos para los periodos activos.</p>
            )}
        </div>
    );
};

export default Dashboard;
