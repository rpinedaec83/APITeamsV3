import React, { createContext, useContext } from 'react';

interface CompanyContextType {
    companyKey: string;
}

const CompanyContext = createContext<CompanyContextType>({ companyKey: '' });

export const CompanyProvider: React.FC<{ companyKey: string; children: React.ReactNode }> = ({ companyKey, children }) => {
    return (
        <CompanyContext.Provider value={{ companyKey }}>
            {children}
        </CompanyContext.Provider>
    );
};

export const useCompanyKey = () => useContext(CompanyContext).companyKey;
