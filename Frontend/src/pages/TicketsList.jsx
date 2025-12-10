import React, { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { ticketsApi } from '../services/api'
import { useAuth } from '../context/AuthContext'
import NewTicketModal from '../components/NewTicketModal'
import './TicketsList.css'

const TicketsList = () => {
  const [tickets, setTickets] = useState([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState('')
  const [searchText, setSearchText] = useState('')
  const [showNewTicketModal, setShowNewTicketModal] = useState(false)
  const { isAuthenticated, logout } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    loadTickets()
  }, [statusFilter, searchText])

  const loadTickets = async () => {
    try {
      setLoading(true)
      const response = await ticketsApi.getAll(statusFilter || null, searchText || null)
      setTickets(response.data)
    } catch (error) {
      console.error('שגיאה בטעינת כרטיסים:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleTicketClick = (ticketId) => {
    navigate(`/tickets/${ticketId}`)
  }

  const handleNewTicketCreated = () => {
    setShowNewTicketModal(false)
    loadTickets()
  }

  const getStatusBadgeClass = (status) => {
    const statusMap = {
      'Open': 'status-open',
      'InProgress': 'status-inprogress',
      'Resolved': 'status-resolved',
      'Closed': 'status-closed'
    }
    return statusMap[status] || 'status-open'
  }

  const getStatusLabel = (status) => {
    const statusMap = {
      'Open': 'פתוח',
      'InProgress': 'בטיפול',
      'Resolved': 'נפתר',
      'Closed': 'סגור'
    }
    return statusMap[status] || status
  }

  return (
    <div className="tickets-list-container">
      <div className="container">
        <div className="page-header">
          <h1>מערכת תמיכה - כל הכרטיסים</h1>
          <div className="header-actions">
            {isAuthenticated() ? (
              <>
                <span className="user-info">מחובר כ-מנהל</span>
                <button className="btn btn-secondary" onClick={logout}>
                  התנתק
                </button>
              </>
            ) : (
              <button className="btn btn-primary" onClick={() => navigate('/login')}>
                התחבר לעריכה
              </button>
            )}
          </div>
        </div>

        <div className="filters">
          <div className="form-group">
            <label>סטטוס:</label>
            <select 
              value={statusFilter} 
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="">הכל</option>
              <option value="Open">פתוח</option>
              <option value="InProgress">בטיפול</option>
              <option value="Resolved">נפתר</option>
              <option value="Closed">סגור</option>
            </select>
          </div>
          
          <div className="form-group">
            <label>חיפוש:</label>
            <input
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder="חפש בשם או בתיאור..."
            />
          </div>
          
          <button 
            className="btn btn-primary"
            onClick={() => setShowNewTicketModal(true)}
          >
            + כרטיס חדש
          </button>
        </div>

        {loading ? (
          <div className="loading">טוען כרטיסים...</div>
        ) : (
          <div className="table-container">
            {tickets.length === 0 ? (
              <p style={{ textAlign: 'center', padding: '20px' }}>
                לא נמצאו כרטיסים
              </p>
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>מספר כרטיס</th>
                    <th>שם מלא</th>
                    <th>אימייל</th>
                    <th>תיאור</th>
                    <th>סיכום AI</th>
                    <th>סטטוס</th>
                    <th>תאריך יצירה</th>
                  </tr>
                </thead>
                <tbody>
                  {tickets.map((ticket) => (
                    <tr 
                      key={ticket.id} 
                      onClick={() => handleTicketClick(ticket.id)}
                    >
                      <td>{ticket.id.substring(0, 8)}...</td>
                      <td>{ticket.fullName}</td>
                      <td>{ticket.email}</td>
                      <td className="description-cell">
                        {ticket.description.length > 50 
                          ? `${ticket.description.substring(0, 50)}...` 
                          : ticket.description}
                      </td>
                      <td>
                        {ticket.aiSummary ? (
                          <span className="badge">יש סיכום</span>
                        ) : (
                          <span style={{ color: '#999' }}>אין</span>
                        )}
                      </td>
                      <td>
                        <span className={`status-badge ${getStatusBadgeClass(ticket.status)}`}>
                          {getStatusLabel(ticket.status)}
                        </span>
                      </td>
                      <td>{new Date(ticket.createdAt).toLocaleDateString('he-IL')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}

        {showNewTicketModal && (
          <NewTicketModal
            onClose={() => setShowNewTicketModal(false)}
            onSuccess={handleNewTicketCreated}
          />
        )}
      </div>
    </div>
  )
}

export default TicketsList

