import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider, OrganizationProvider } from './contexts';
import App from './App';
import './styles.css';
ReactDOM.createRoot(document.getElementById('root')!).render(
  <BrowserRouter><AuthProvider><OrganizationProvider><App /></OrganizationProvider></AuthProvider></BrowserRouter>
);
