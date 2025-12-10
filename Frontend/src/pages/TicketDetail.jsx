import React, { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ticketsApi } from '../services/api'
import { useAuth } from '../context/AuthContext'
import './TicketDetail.css'

const TicketDetail = () => {
  const { id } = useParams()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuth()
  const [ticket, setTicket] = useState(null)
  const [loading, setLoading] = useState(true)
  const [editing, setEditing] = useState(false)
  const [status, setStatus] = useState('')
  const [resolution, setResolution] = useState('')
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState({ type: '', text: '' })

  useEffect(() => {
    loadTicket()
  }, [id])

  const loadTicket = async () => {
    try {
      setLoading(true)
      const response = await ticketsApi.getById(id)
      const ticketData = response.data
      setTicket(ticketData)
      setStatus(ticketData.status)
      setResolution(ticketData.resolution || '')
    } catch (error) {
      console.error('שגיאה בטעינת כרטיס:', error)
      setMessage({ type: 'error', text: 'שגיאה בטעינת הכרטיס' })
    } finally {
      setLoading(false)
    }
  }

  const handleSave = async () => {
    if (!isAuthenticated()) {
      setMessage({ type: 'error', text: 'יש להתחבר כדי לערוך כרטיסים' })
      return
    }

    try {
      setSaving(true)
      setMessage({ type: '', text: '' })
      
      const updateData = {}
      if (status !== ticket.status) {
        updateData.status = status
      }
      if (resolution !== (ticket.resolution || '')) {
        updateData.resolution = resolution
      }

      await ticketsApi.update(id, updateData)
      setMessage({ type: 'success', text: 'הכרטיס עודכן בהצלחה!' })
      setEditing(false)
      await loadTicket()
    } catch (error) {
      setMessage({ 
        type: 'error', 
        text: error.response?.data?.message || 'שגיאה בעדכון הכרטיס' 
      })
    } finally {
      setSaving(false)
    }
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

  if (loading) {
    return (
      <div className="ticket-detail-container">
        <div className="container">
          <div className="loading">טוען כרטיס...</div>
        </div>
      </div>
    )
  }

  if (!ticket) {
    return (
      <div className="ticket-detail-container">
        <div className="container">
          <div className="error-message">כרטיס לא נמצא</div>
          <button className="btn btn-primary" onClick={() => navigate('/tickets')}>
            חזרה לרשימה
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="ticket-detail-container">
      <div className="container">
        <div className="ticket-detail-header">
          <button className="btn btn-secondary" onClick={() => navigate('/tickets')}>
            ← חזרה לרשימה
          </button>
          {isAuthenticated() && !editing && (
            <button className="btn btn-primary" onClick={() => setEditing(true)}>
              ערוך כרטיס
            </button>
          )}
        </div>

        {message.text && (
          <div className={message.type === 'error' ? 'error-message' : 'success-message'}>
            {message.text}
          </div>
        )}

        <div className="card">
          <div className="card-header">
            <h2 className="card-title">כרטיס תמיכה #{ticket.id.substring(0, 8)}</h2>
            <span className={`status-badge ${getStatusBadgeClass(ticket.status)}`}>
              {getStatusLabel(ticket.status)}
            </span>
          </div>

          <div className="card-body">
            <div className="detail-row">
              <label>שם מלא:</label>
              <div>{ticket.fullName}</div>
            </div>

            <div className="detail-row">
              <label>אימייל:</label>
              <div>{ticket.email}</div>
            </div>

            <div className="detail-row">
              <label>תיאור הבעיה:</label>
              <div className="description-box">{ticket.description}</div>
            </div>

            {ticket.aiSummary && (
              <div className="detail-row">
                <label>סיכום AI:</label>
                <div className="ai-summary-box">{ticket.aiSummary}</div>
              </div>
            )}

            {editing && isAuthenticated() ? (
              <>
                <div className="form-group">
                  <label>סטטוס:</label>
                  <select 
                    value={status} 
                    onChange={(e) => setStatus(e.target.value)}
                  >
                    <option value="Open">פתוח</option>
                    <option value="InProgress">בטיפול</option>
                    <option value="Resolved">נפתר</option>
                    <option value="Closed">סגור</option>
                  </select>
                </div>

                <div className="form-group">
                  <label>פתרון:</label>
                  <textarea
                    value={resolution}
                    onChange={(e) => setResolution(e.target.value)}
                    placeholder="הכנס פתרון לבעיה..."
                    rows="5"
                  />
                </div>

                <div className="card-footer">
                  <button 
                    className="btn btn-success" 
                    onClick={handleSave}
                    disabled={saving}
                  >
                    {saving ? 'שומר...' : 'שמור שינויים'}
                  </button>
                  <button 
                    className="btn btn-secondary" 
                    onClick={() => {
                      setEditing(false)
                      setStatus(ticket.status)
                      setResolution(ticket.resolution || '')
                      setMessage({ type: '', text: '' })
                    }}
                  >
                    ביטול
                  </button>
                </div>
              </>
            ) : (
              <>
                {ticket.resolution && (
                  <div className="detail-row">
                    <label>פתרון:</label>
                    <div className="resolution-box">{ticket.resolution}</div>
                  </div>
                )}

                {!isAuthenticated() && (
                  <div className="info-box">
                    <p>כדי לערוך כרטיסים, יש להתחבר כמנהל</p>
                    <button 
                      className="btn btn-primary" 
                      onClick={() => navigate('/login')}
                    >
                      התחבר
                    </button>
                  </div>
                )}
              </>
            )}

            <div className="detail-row">
              <label>תאריך יצירה:</label>
              <div>{new Date(ticket.createdAt).toLocaleString('he-IL')}</div>
            </div>

            <div className="detail-row">
              <label>תאריך עדכון אחרון:</label>
              <div>{new Date(ticket.updatedAt).toLocaleString('he-IL')}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default TicketDetail

