import { Navigate, Route, Routes } from 'react-router-dom';
import TicketsPage from './pages/TicketsPage';
import TicketDetailPage from './pages/TicketDetailPage';
import LoginPage from './pages/LoginPage';

/**
 * The three screens the README asks for.
 *
 * The detail route is `/tickets/:id` because the exercise requires a ticket to
 * be reachable by its unique id — it is also the link the confirmation email
 * sends the customer, so it has to work on a cold page load with no app state.
 */
export default function App() {
  return (
    <Routes>
      <Route path="/" element={<TicketsPage />} />
      <Route path="/tickets/:id" element={<TicketDetailPage />} />
      <Route path="/login" element={<LoginPage />} />

      {/* Anything else goes to the list rather than a dead end. */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
