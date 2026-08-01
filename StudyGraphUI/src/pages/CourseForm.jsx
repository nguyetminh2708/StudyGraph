import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { get, post, put } from '../api'

const CATEGORIES = ['Database', 'Backend', 'Frontend', 'DevOps']

export default function CourseForm() {
  const { key } = useParams()
  const editing = key != null
  const navigate = useNavigate()
  const [form, setForm] = useState({ key: '', title: '', category: 'Database', level: 1, description: '', tags: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!editing) return
    let cancelled = false
    get(`/api/courses/${key}`)
      .then((d) => {
        if (cancelled) return
        const c = d.course
        setForm({
          key: c.key,
          title: c.title,
          category: c.category,
          level: c.level,
          description: c.description ?? '',
          tags: (c.tags ?? []).join(', ')
        })
      })
      .catch((err) => {
        if (!cancelled) setError(err.message)
      })
    return () => {
      cancelled = true
    }
  }, [editing, key])

  const set = (field) => (e) => setForm({ ...form, [field]: e.target.value })

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true)
    setError('')
    const body = {
      Key: form.key.trim() || null,
      Title: form.title.trim(),
      Category: form.category,
      Level: Number(form.level),
      Description: form.description,
      Tags: form.tags
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean)
    }
    try {
      const saved = editing ? await put(`/api/courses/${key}`, body) : await post('/api/courses', body)
      navigate(`/courses/${saved.key}`)
    } catch (err) {
      setError(err.message)
      setBusy(false)
    }
  }

  return (
    <form className="course-form" onSubmit={submit}>
      <h1>{editing ? 'Sửa khóa học' : 'Thêm khóa học'}</h1>
      {!editing && (
        <label>
          Mã khóa (tùy chọn — vd c-sql-101, bỏ trống sẽ tự sinh)
          <input value={form.key} onChange={set('key')} placeholder="c-ten-khoa-101" />
        </label>
      )}
      <label>
        Tên khóa học
        <input value={form.title} onChange={set('title')} required />
      </label>
      <label>
        Danh mục
        <select value={form.category} onChange={set('category')}>
          {CATEGORIES.map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
      </label>
      <label>
        Trình độ
        <select value={form.level} onChange={set('level')}>
          <option value={1}>1 — Cơ bản</option>
          <option value={2}>2 — Trung cấp</option>
          <option value={3}>3 — Nâng cao</option>
        </select>
      </label>
      <label>
        Mô tả
        <textarea value={form.description} onChange={set('description')} rows={4} />
      </label>
      <label>
        Tags (phân cách bằng dấu phẩy)
        <input value={form.tags} onChange={set('tags')} placeholder="sql, beginner" />
      </label>
      {error && <p className="form-error">{error}</p>}
      <div className="form-actions">
        <button type="submit" disabled={busy}>
          {busy ? 'Đang lưu…' : editing ? 'Lưu thay đổi' : 'Tạo khóa học'}
        </button>
        <button type="button" className="ghost" onClick={() => navigate(-1)}>
          Hủy
        </button>
      </div>
    </form>
  )
}
