import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App.tsx';
import './index.css';

import { FluentProvider, webLightTheme } from '@fluentui/react-components';

import { getBaseApiUrl } from './utils/config';

const init = async () => {
  try {
    const apiUrl = getBaseApiUrl();
    const response = await fetch(`${apiUrl}/public/spa-config`);
    if (!response.ok) {
      throw new Error(`Failed to load config: ${response.statusText}`);
    }
    const spaConfig = await response.json();

    ReactDOM.createRoot(document.getElementById('root')!).render(
      <React.StrictMode>
        <FluentProvider theme={webLightTheme}>
          <App spaConfig={spaConfig} />
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
