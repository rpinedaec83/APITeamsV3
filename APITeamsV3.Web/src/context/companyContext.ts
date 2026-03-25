import { createContext } from 'react';

export interface CompanyContextType {
    companyKey: string;
}

export const CompanyContext = createContext<CompanyContextType>({ companyKey: '' });
