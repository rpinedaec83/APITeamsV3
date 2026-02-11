import React, { useEffect, useState } from 'react';
import { useApiClient } from '../hooks/useApiClient';
import { useMsal } from "@azure/msal-react";

interface Section {
    idSeccion: number;
    cursoNombre: string;
    // Add other fields
}

// Placeholder for Dashboard
const Dashboard: React.FC = () => {
    const { accounts } = useMsal();
    const api = useApiClient();
    const [status, setStatus] = useState<string>("Loading...");
    const [sections, setSections] = useState<Section[]>([]);

    useEffect(() => {
        setStatus("Fetching data...");
        api.get('/sections')
            .then(res => {
                setSections(res.data.data);
                setStatus("Ready");
            })
            .catch(err => {
                console.error(err);
                setStatus("Error loading sections");
            });
    }, [api]); // api dependency is stable

    return (
        <div style={{ padding: 20 }}>
            <h1>Teams Provisioning Dashboard</h1>
            <p>Welcome, {accounts[0]?.name} ({accounts[0]?.username})</p>
            <p><strong>System Status:</strong> {status}</p>

            <div style={{ marginTop: 20, display: 'flex', gap: '10px' }}>
                <a href="/operations" style={{ textDecoration: 'none' }}>
                    <button style={{ padding: '10px 20px', cursor: 'pointer' }}>Go to Operations Dashboard</button>
                </a>
                <a href="/admin/company-configs" style={{ textDecoration: 'none' }}>
                    <button style={{ padding: '10px 20px', cursor: 'pointer' }}>Manage Company Configs</button>
                </a>
            </div>

            <div style={{ marginTop: 20 }}>
                <h2>Create Team</h2>
                {/* Form to provision team manually */}
            </div>

            <div style={{ marginTop: 20 }}>
                <h2>Sections</h2>
                {sections.length === 0 ? <p>No sections found.</p> : (
                    <ul>
                        {sections.map((s, i) => (
                            <li key={i}>{JSON.stringify(s)}</li>
                        ))}
                    </ul>
                )}
            </div>
        </div>
    );
};

export default Dashboard;
