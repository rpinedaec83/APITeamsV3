import React from "react";
import { useMsal } from "@azure/msal-react";
import { InteractionStatus, BrowserAuthError } from "@azure/msal-browser";
import { getLoginRequest } from "../authConfig";
import { Button, Card, CardHeader, CardPreview, Text, Spinner, makeStyles, tokens } from "@fluentui/react-components";
import { LockClosedRegular } from "@fluentui/react-icons";

const useStyles = makeStyles({
    container: {
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        height: "100vh",
        backgroundColor: tokens.colorNeutralBackground2,
    },
    card: {
        width: "400px",
        padding: "20px",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: "20px",
        boxShadow: tokens.shadow16,
    },
    logo: {
        fontSize: "48px",
        color: tokens.colorBrandForeground1,
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

    // Show loading spinner while MSAL is processing a redirect or other interaction
    if (inProgress !== InteractionStatus.None) {
        return (
            <div className={styles.container}>
                <Card className={styles.card}>
                    <Spinner label="Signing in..." size="large" />
                </Card>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <Card className={styles.card}>
                <CardHeader
                    image={<LockClosedRegular className={styles.logo} />}
                    header={
                        <Text weight="semibold" size={600}>
                            Sign In to Teams Admin
                        </Text>
                    }
                    description={<Text>Please sign in with your organization account to access the dashboard.</Text>}
                />
                <CardPreview>
                    {/* Optional image or graphic */}
                </CardPreview>

                <Button appearance="primary" size="large" onClick={handleLogin}>
                    Sign In
                </Button>
            </Card>
        </div>
    );
};

