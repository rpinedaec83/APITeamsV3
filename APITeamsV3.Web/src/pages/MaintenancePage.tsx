import React from 'react';
import {
    makeStyles,
    tokens,
    shorthands,
    Button,
    Title1,
    Subtitle1,
    Avatar
} from '@fluentui/react-components';
import {
    WrenchRegular,
    WarningRegular,
    SignOutRegular
} from '@fluentui/react-icons';
import { useMsal } from "@azure/msal-react";

interface MaintenancePageProps {
    message?: string;
}

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        width: '100vw',
        backgroundColor: '#f4efe6',
        backgroundImage: 'radial-gradient(circle at top, #fff4d6 0%, #f4efe6 45%, #ebe3d5 100%)',
        ...shorthands.padding('24px'),
        boxSizing: 'border-box',
    },
    card: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        backgroundColor: '#fffdf8',
        ...shorthands.padding('44px'),
        ...shorthands.borderRadius(tokens.borderRadiusXLarge),
        boxShadow: tokens.shadow16,
        maxWidth: '560px',
        gap: '20px',
        border: '1px solid #e7d9be',
    },
    iconWrap: {
        position: 'relative',
        width: '96px',
        height: '96px',
        display: 'grid',
        placeItems: 'center',
        borderRadius: '50%',
        backgroundColor: '#fff1cc',
        color: '#8a5a00',
    },
    icon: {
        fontSize: '52px',
    },
    badge: {
        position: 'absolute',
        right: '-4px',
        bottom: '-2px',
        width: '34px',
        height: '34px',
        display: 'grid',
        placeItems: 'center',
        borderRadius: '50%',
        backgroundColor: '#c50f1f',
        color: '#ffffff',
        boxShadow: tokens.shadow8,
    },
    userInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        ...shorthands.padding('12px', '16px'),
        backgroundColor: '#f6efe2',
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        width: '100%',
        justifyContent: 'center',
    },
    message: {
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.6',
    },
    note: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
});

const MaintenancePage: React.FC<MaintenancePageProps> = ({ message }) => {
    const styles = useStyles();
    const { instance, accounts } = useMsal();
    const account = accounts[0];

    const handleSignOut = () => {
        instance.logoutRedirect({
            postLogoutRedirectUri: "/",
        });
    };

    return (
        <div className={styles.root}>
            <div className={styles.card}>
                <div className={styles.iconWrap}>
                    <WrenchRegular className={styles.icon} />
                    <div className={styles.badge}>
                        <WarningRegular />
                    </div>
                </div>

                <Title1>Sistema en mantenimiento</Title1>

                <div className={styles.userInfo}>
                    <Avatar name={account?.name || "Usuario"} size={32} color="colorful" />
                    <div style={{ textAlign: 'left' }}>
                        <div style={{ fontWeight: 600, fontSize: '14px' }}>{account?.name}</div>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground4 }}>{account?.username}</div>
                    </div>
                </div>

                <Subtitle1 className={styles.message}>
                    {message || "El sistema se encuentra temporalmente fuera de servicio por mantenimiento."}
                </Subtitle1>

                <div className={styles.note}>
                    Cuando el mantenimiento finalice, recargue la página para continuar.
                </div>

                <Button
                    appearance="primary"
                    size="large"
                    icon={<SignOutRegular />}
                    onClick={handleSignOut}
                    style={{ width: '100%' }}
                >
                    Cerrar Sesión
                </Button>
            </div>
        </div>
    );
};

export default MaintenancePage;
