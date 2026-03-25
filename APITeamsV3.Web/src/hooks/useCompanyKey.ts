import { useContext } from 'react';
import { CompanyContext } from '../context/companyContext';

export const useCompanyKey = () => useContext(CompanyContext).companyKey;
