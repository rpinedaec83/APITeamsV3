import React from "react";
import { useMsal } from "@azure/msal-react";
import { InteractionStatus, BrowserAuthError } from "@azure/msal-browser";
import { getLoginRequest } from "../authConfig";
import {
    Badge,
    Button,
    Card,
    Spinner,
    Text,
    Title1,
    Title3,
    makeStyles,
    shorthands,
    tokens,
} from "@fluentui/react-components";
import {
    ArrowRightRegular,
    BoardRegular,
    CalendarLtrRegular,
    LockClosedRegular,
    PeopleTeamRegular,
    PlayCircleRegular,
} from "@fluentui/react-icons";

const useStyles = makeStyles({
    root: {
        minHeight: "100vh",
        display: "grid",
        gridTemplateColumns: "minmax(0, 1.25fr) minmax(420px, 0.9fr)",
        background: "linear-gradient(135deg, #0f172a 0%, #11253f 42%, #133b5c 100%)",
        position: "relative",
        overflow: "hidden",
        "@media (max-width: 1100px)": {
            gridTemplateColumns: "1fr",
        },
    },
    ambientTop: {
        position: "absolute",
        insetBlockStart: "-160px",
        insetInlineStart: "-120px",
        width: "480px",
        height: "480px",
        borderRadius: "999px",
        background: "radial-gradient(circle, rgba(0,192,239,0.26) 0%, rgba(0,192,239,0) 72%)",
        pointerEvents: "none",
    },
    ambientBottom: {
        position: "absolute",
        insetBlockEnd: "-220px",
        insetInlineEnd: "-120px",
        width: "520px",
        height: "520px",
        borderRadius: "999px",
        background: "radial-gradient(circle, rgba(243,156,18,0.22) 0%, rgba(243,156,18,0) 72%)",
        pointerEvents: "none",
    },
    panel: {
        position: "relative",
        zIndex: 1,
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        ...shorthands.padding("48px"),
        "@media (max-width: 700px)": {
            ...shorthands.padding("28px", "22px"),
        },
    },
    brandBlock: {
        display: "flex",
        flexDirection: "column",
        gap: "18px",
        maxWidth: "700px",
    },
    brandChip: {
        width: "fit-content",
        color: "#e6f6fb",
        backgroundColor: "rgba(0,192,239,0.12)",
        border: "1px solid rgba(0,192,239,0.22)",
        ...shorthands.borderRadius("999px"),
        ...shorthands.padding("8px", "14px"),
    },
    headline: {
        color: "#f8fbff",
        fontSize: "clamp(2.8rem, 5vw, 4.8rem)",
        lineHeight: 1,
        letterSpacing: "-0.05em",
        margin: 0,
        maxWidth: "12ch",
    },
    subheadline: {
        color: "rgba(230, 241, 252, 0.82)",
        fontSize: tokens.fontSizeBase500,
        lineHeight: 1.6,
        maxWidth: "60ch",
    },
    highlights: {
        display: "grid",
        gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
        gap: "16px",
        marginTop: "14px",
        "@media (max-width: 900px)": {
            gridTemplateColumns: "1fr",
        },
    },
    highlightCard: {
        backgroundColor: "rgba(255,255,255,0.06)",
        border: "1px solid rgba(255,255,255,0.08)",
        boxShadow: "0 22px 48px rgba(8, 15, 28, 0.18)",
        backdropFilter: "blur(12px)",
        ...shorthands.borderRadius("18px"),
        ...shorthands.padding("18px"),
        display: "flex",
        flexDirection: "column",
        gap: "10px",
    },
    highlightIcon: {
        width: "44px",
        height: "44px",
        display: "grid",
        placeItems: "center",
        color: "#7dd3fc",
        backgroundColor: "rgba(125,211,252,0.12)",
        ...shorthands.borderRadius("14px"),
    },
    highlightTitle: {
        color: "#f8fbff",
        fontWeight: 700,
    },
    highlightText: {
        color: "rgba(230, 241, 252, 0.72)",
        lineHeight: 1.5,
    },
    footerMeta: {
        display: "flex",
        flexWrap: "wrap",
        gap: "10px",
        marginTop: "28px",
    },
    loginPanel: {
        position: "relative",
        zIndex: 1,
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        ...shorthands.padding("32px"),
        "@media (max-width: 1100px)": {
            minHeight: "auto",
            paddingTop: "0",
            paddingBottom: "40px",
        },
        "@media (max-width: 700px)": {
            ...shorthands.padding("0", "22px", "30px"),
        },
    },
    loginCard: {
        width: "100%",
        maxWidth: "460px",
        display: "flex",
        flexDirection: "column",
        gap: "22px",
        background: "linear-gradient(180deg, rgba(255,255,255,0.96) 0%, rgba(248,250,252,0.98) 100%)",
        border: "1px solid rgba(255,255,255,0.55)",
        boxShadow: "0 28px 64px rgba(6, 14, 28, 0.22)",
        ...shorthands.borderRadius("28px"),
        ...shorthands.padding("28px"),
    },
    loginHeader: {
        display: "flex",
        alignItems: "center",
        gap: "16px",
    },
    loginIconWrap: {
        width: "64px",
        height: "64px",
        display: "grid",
        placeItems: "center",
        color: "#0f6cbd",
        background: "linear-gradient(135deg, rgba(15,108,189,0.12) 0%, rgba(0,192,239,0.08) 100%)",
        ...shorthands.borderRadius("20px"),
    },
    loginTitleBlock: {
        display: "flex",
        flexDirection: "column",
        gap: "6px",
    },
    loginMeta: {
        display: "grid",
        gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
        gap: "12px",
        "@media (max-width: 520px)": {
            gridTemplateColumns: "1fr",
        },
    },
    metaCard: {
        backgroundColor: "#f3f7fb",
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ...shorthands.borderRadius("16px"),
        ...shorthands.padding("14px"),
        display: "flex",
        flexDirection: "column",
        gap: "6px",
    },
    actionButton: {
        minHeight: "52px",
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
    },
    loadingWrap: {
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        minHeight: "100vh",
        background: "linear-gradient(135deg, #0f172a 0%, #11253f 42%, #133b5c 100%)",
        ...shorthands.padding("24px"),
    },
    loadingCard: {
        width: "100%",
        maxWidth: "360px",
        ...shorthands.borderRadius("24px"),
        ...shorthands.padding("28px"),
        boxShadow: "0 24px 48px rgba(8, 15, 28, 0.26)",
    },
});

export const Login: React.FC = () => {
    const { instance, inProgress } = useMsal();
    const styles = useStyles();

    const handleLogin = () => {
        if (inProgress !== InteractionStatus.None) {
            console.warn("Login skipped: interaction already in progress, status:", inProgress);
            return;
        }

        instance.loginRedirect(getLoginRequest()).catch((e) => {
            if (e instanceof BrowserAuthError && e.errorCode === "interaction_in_progress") {
                console.warn("Login skipped: interaction_in_progress (race condition).");
            } else {
                console.error("Login failed:", e);
            }
        });
    };

    if (inProgress !== InteractionStatus.None) {
        return (
            <div className={styles.loadingWrap}>
                <Card className={styles.loadingCard}>
                    <Spinner label="Validando acceso organizacional..." size="large" />
                </Card>
            </div>
        );
    }

    return (
        <div className={styles.root}>
            <div className={styles.ambientTop} />
            <div className={styles.ambientBottom} />

            <section className={styles.panel}>
                <div className={styles.brandBlock}>
                    <div className={styles.brandChip}>Teams Admin Platform</div>
                    <h1 className={styles.headline}>Orquesta secciones, agendas y grabaciones con criterio operativo.</h1>
                    <Text className={styles.subheadline}>
                        Una consola pensada para seguimiento académico real: sincronización de Teams,
                        control de roster, sesiones, evidencias y diagnóstico por tenant.
                    </Text>

                    <div className={styles.highlights}>
                        <div className={styles.highlightCard}>
                            <div className={styles.highlightIcon}><BoardRegular fontSize={24} /></div>
                            <Text className={styles.highlightTitle}>Cobertura por tenant</Text>
                            <Text className={styles.highlightText}>
                                Visualiza sedes activas, programas críticos y salud operativa sin salir del panel.
                            </Text>
                        </div>
                        <div className={styles.highlightCard}>
                            <div className={styles.highlightIcon}><PeopleTeamRegular fontSize={24} /></div>
                            <Text className={styles.highlightTitle}>Roster bajo control</Text>
                            <Text className={styles.highlightText}>
                                Detecta altas, bajas y consistencia de miembros antes de que afecten clases y grabaciones.
                            </Text>
                        </div>
                        <div className={styles.highlightCard}>
                            <div className={styles.highlightIcon}><PlayCircleRegular fontSize={24} /></div>
                            <Text className={styles.highlightTitle}>Grabaciones trazables</Text>
                            <Text className={styles.highlightText}>
                                Automatiza búsqueda, transferencia y ordenamiento de evidencia en Teams por sección.
                            </Text>
                        </div>
                    </div>
                </div>

                <div className={styles.footerMeta}>
                    <Badge appearance="filled" color="informative">Microsoft 365 protegido</Badge>
                    <Badge appearance="outline">Multi-tenant</Badge>
                    <Badge appearance="outline">Pilot-ready</Badge>
                </div>
            </section>

            <aside className={styles.loginPanel}>
                <Card className={styles.loginCard}>
                    <div className={styles.loginHeader}>
                        <div className={styles.loginIconWrap}>
                            <LockClosedRegular fontSize={30} />
                        </div>
                        <div className={styles.loginTitleBlock}>
                            <Title1 style={{ margin: 0 }}>Ingreso seguro</Title1>
                            <Text>Accede con tu cuenta institucional para administrar el tenant actual.</Text>
                        </div>
                    </div>

                    <div className={styles.loginMeta}>
                        <div className={styles.metaCard}>
                            <Text size={200} weight="semibold">Acceso</Text>
                            <Title3 style={{ margin: 0 }}>SSO organizacional</Title3>
                            <Text>Autenticación con Microsoft Entra ID y control por roles.</Text>
                        </div>
                        <div className={styles.metaCard}>
                            <Text size={200} weight="semibold">Alcance</Text>
                            <Title3 style={{ margin: 0 }}>Operación académica</Title3>
                            <Text>Teams, agendas, roster, reportes y transferencia de grabaciones.</Text>
                        </div>
                    </div>

                    <div className={styles.metaCard}>
                        <Text size={200} weight="semibold">Qué puedes hacer aquí</Text>
                        <Text>
                            Supervisar provisión, revisar cobertura, ejecutar jobs de sincronización y monitorear el estado del servicio.
                        </Text>
                    </div>

                    <Button
                        appearance="primary"
                        size="large"
                        className={styles.actionButton}
                        icon={<ArrowRightRegular />}
                        iconPosition="after"
                        onClick={handleLogin}
                    >
                        Entrar al panel
                    </Button>

                    <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                        <CalendarLtrRegular color="#3c8dbc" />
                        <Text size={200}>
                            Si tu sesión ya está activa en Microsoft 365, el acceso será inmediato.
                        </Text>
                    </div>
                </Card>
            </aside>
        </div>
    );
};
