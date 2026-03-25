import React from 'react';
import { CompanyContext } from './context/companyContext';

export const CompanyProvider: React.FC<{ companyKey: string; children: React.ReactNode }> = ({ companyKey, children }) => {
    return (
        <CompanyContext.Provider value={{ companyKey }}>
            {children}
        </CompanyContext.Provider>
    );
};
