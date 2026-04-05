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
    ShieldLockRegular,
    SignOutRegular,
    PersonQuestionMarkRegular
} from '@fluentui/react-icons';
import { useMsal } from "@azure/msal-react";

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100vh',
        width: '100vw',
        backgroundColor: tokens.colorNeutralBackground2,
        backgroundImage: `radial-gradient(circle at 50% 50%, ${tokens.colorBrandBackground2} 0%, ${tokens.colorNeutralBackground2} 100%)`,
        ...shorthands.padding('24px'),
        boxSizing: 'border-box',
    },
    card: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.padding('48px'),
        ...shorthands.borderRadius(tokens.borderRadiusXLarge),
        boxShadow: tokens.shadow16,
        maxWidth: '500px',
        gap: '24px',
    },
    iconContainer: {
        position: 'relative',
        marginBottom: '12px',
    },
    lockIcon: {
        fontSize: '80px',
        color: tokens.colorPaletteRedForeground1,
    },
    badge: {
        position: 'absolute',
        bottom: '0',
        right: '-10px',
        backgroundColor: tokens.colorPaletteYellowBackground3,
        ...shorthands.padding('4px'),
        ...shorthands.borderRadius('50%'),
        boxShadow: tokens.shadow4,
    },
    message: {
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.5',
    },
    footer: {
        marginTop: '12px',
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        width: '100%',
    },
    userInfo: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        ...shorthands.padding('12px'),
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        width: '100%',
        justifyContent: 'center',
    }
});

const NoAccessPage: React.FC = () => {
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
                <div className={styles.iconContainer}>
                    <ShieldLockRegular className={styles.lockIcon} />
                    <div className={styles.badge}>
                        <PersonQuestionMarkRegular fontSize={32} color={tokens.colorPaletteYellowForeground1} />
                    </div>
                </div>

                <Title1>Acceso Restringido</Title1>
                
                <div className={styles.userInfo}>
                    <Avatar name={account?.name || "Usuario"} size={32} color="colorful" />
                    <div style={{ textAlign: 'left' }}>
                        <div style={{ fontWeight: 600, fontSize: '14px' }}>{account?.name}</div>
                        <div style={{ fontSize: '12px', color: tokens.colorNeutralForeground4 }}>{account?.username}</div>
                    </div>
                </div>

                <Subtitle1 className={styles.message}>
                    Usted no tiene roles asignados en el sistema de Aprovisionamiento de Teams de <b>Idat/Zegel</b>.
                    <br /><br />
                    Por favor, realice su solicitud de acceso al área de <b>TI (Service Desk)</b> para poder continuar.
                </Subtitle1>

                <div className={styles.footer}>
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
        </div>
    );
};

export default NoAccessPage;
