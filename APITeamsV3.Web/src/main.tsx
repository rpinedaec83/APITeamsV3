import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App.tsx';
import './index.css';

import { FluentProvider, webLightTheme } from '@fluentui/react-components';

const init = async () => {
  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <FluentProvider theme={webLightTheme}>
        <App />
      </FluentProvider>
    </React.StrictMode>,
  );
};

init();
