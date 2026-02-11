import React from 'react';
import {
    makeStyles,
    tokens,
    shorthands,
    Title3,
    Avatar,
    LargeTitle,
    Button
} from '@fluentui/react-components';
import {
    GridDotsRegular,
    HomeRegular,
    SettingsRegular,
    OrganizationRegular,
    SignOutRegular,
    AlertRegular
} from '@fluentui/react-icons';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        height: '100vh',
        width: '100vw',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    sidebar: {
        width: '260px',
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'column',
        borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
        flexShrink: 0,
        boxShadow: tokens.shadow4,
        zIndex: 2,
    },
    sidebarHeader: {
        height: '60px',
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        ...shorthands.padding('0', '20px'),
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    brand: {
        fontWeight: tokens.fontWeightBold,
        color: tokens.colorBrandForeground1,
        fontSize: tokens.fontSizeBase400,
    },
    navContainer: {
        flex: 1,
        padding: '20px 10px',
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    navItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        padding: '10px 16px',
        borderRadius: tokens.borderRadiusMedium,
        cursor: 'pointer',
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase300,
        transition: 'all 0.1s',
        border: 'none',
        backgroundColor: 'transparent',
        textAlign: 'left',
        width: '100%',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
            color: tokens.colorNeutralForeground1,
        }
    },
    activeNavItem: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground1,
        fontWeight: tokens.fontWeightSemibold,
        ':hover': {
            backgroundColor: tokens.colorBrandBackground2,
        }
    },
    footer: {
        padding: '20px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    content: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    },
    header: {
        height: '60px',
        backgroundColor: tokens.colorNeutralBackground1,
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'flex-end',
        padding: '0 32px',
        flexShrink: 0,
        boxShadow: tokens.shadow2,
        zIndex: 1,
    },
    mainScroll: {
        flex: 1,
        overflowY: 'auto',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    userProfile: {
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
    }
});

const MainLayout: React.FC = () => {
    const styles = useStyles();
    const navigate = useNavigate();
    const location = useLocation();

    const menuItems = [
        { label: 'Dashboard', icon: <HomeRegular />, path: '/' },
        { label: 'Operations', icon: <OrganizationRegular />, path: '/operations' },
        { label: 'Company Configs', icon: <SettingsRegular />, path: '/admin/company-configs' },
    ];

    return (
        <div className={styles.root}>
            {/* Sidebar */}
            <div className={styles.sidebar}>
                <div className={styles.sidebarHeader}>
                    <GridDotsRegular fontSize={24} color={tokens.colorBrandForeground1} />
                    <span className={styles.brand}>Teams Admin</span>
                </div>

                <nav className={styles.navContainer}>
                    {menuItems.map((item) => {
                        const isActive = location.pathname === item.path;
                        return (
                            <button
                                key={item.path}
                                className={`${styles.navItem} ${isActive ? styles.activeNavItem : ''}`}
                                onClick={() => navigate(item.path)}
                            >
                                {item.icon}
                                <span>{item.label}</span>
                            </button>
                        )
                    })}
                </nav>

                <div className={styles.footer}>
                    <Button icon={<SignOutRegular />} appearance="subtle">Sign Out</Button>
                </div>
            </div>

            {/* Main Content Area */}
            <div className={styles.content}>
                <header className={styles.header}>
                    <div className={styles.userProfile}>
                        <div style={{ textAlign: 'right', display: 'flex', flexDirection: 'column' }}>
                            <span style={{ fontWeight: 600, fontSize: '14px' }}>Facundo Arana</span>
                            <span style={{ fontSize: '12px', color: tokens.colorNeutralForeground3 }}>Admin</span>
                        </div>
                        <Avatar name="Facundo Arana" color="colorful" />
                    </div>
                </header>

                <main className={styles.mainScroll}>
                    {/* The routed page content will appear here */}
                    <Outlet />
                </main>
            </div>
        </div>
    );
};

export default MainLayout;
