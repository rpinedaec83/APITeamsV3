import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App.tsx';
import './index.css';

import { FluentProvider, webLightTheme } from '@fluentui/react-components';

import type { SpaBootstrapConfig } from './authConfig';
import { getBaseApiUrl } from './utils/config';

const init = async () => {
  try {
    const apiUrl = getBaseApiUrl();
    const response = await fetch(`${apiUrl}/public/spa-config`);
    if (!response.ok) {
      throw new Error(`Failed to load config: ${response.statusText}`);
    }
    const spaConfig = await response.json() as SpaBootstrapConfig;
    window.__APITEAMSV3_CONFIG__ = spaConfig;

    const { PublicClientApplication } = await import("@azure/msal-browser");
    const { createMsalConfig, clearStaleInteractionState } = await import("./authConfig");
    
    // Only clear stale interaction state if we are NOT returning from a redirect
    // (i.e., URL does not contain auth code/state parameters)
    const urlHasAuthResponse = window.location.hash.includes("code=") || 
                                window.location.search.includes("code=");
    if (!urlHasAuthResponse) {
      clearStaleInteractionState();
    }

    const msalInstance = new PublicClientApplication(
      createMsalConfig(spaConfig.spaClientId, spaConfig.tenantId, window.location.origin)
    );
    await msalInstance.initialize();

    ReactDOM.createRoot(document.getElementById('root')!).render(
      <React.StrictMode>
        <FluentProvider theme={webLightTheme}>
          <App spaConfig={spaConfig} msalInstance={msalInstance} />
        </FluentProvider>
      </React.StrictMode>,
    );
  } catch (error) {
    console.error("Failed to initialize app:", error);
    ReactDOM.createRoot(document.getElementById('root')!).render(
      <div style={{ padding: 20, color: 'red' }}>
        <h1>Application Error</h1>
        <p>Failed to load configuration. Please try again later.</p>
        <pre>{(error as Error).message}</pre>
      </div>
    );
  }
};

init();
