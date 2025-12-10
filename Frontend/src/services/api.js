import axios from 'axios'

const API_BASE_URL = 'http://localhost:5000/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

export const ticketsApi = {
  getAll: (status, search) => {
    const params = {}
    if (status) params.status = status
    if (search) params.search = search
    return api.get('/tickets', { params })
  },
  
  getById: (id) => api.get(`/tickets/${id}`),
  
  create: (data) => api.post('/tickets', data),
  
  update: (id, data) => api.put(`/tickets/${id}`, data)
}

export const authApi = {
  login: (username, password) => 
    api.post('/auth/login', { username, password }),
  
  validate: () => api.get('/auth/validate')
}

export default api

