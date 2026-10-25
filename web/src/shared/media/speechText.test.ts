import { prepareTextForSpeech } from './speechText'

describe('prepareTextForSpeech', () => {
  it.each([
    ['HTTP define los métodos.', 'hache te te pe define los métodos.'],
    ['Usa HTTPS siempre.', 'Usa hache te te pe ese siempre.'],
    ['Una API REST con JSON.', 'Una api rest con yéison.'],
    ['Consulta la URL del recurso.', 'Consulta la u erre ele del recurso.'],
    ['Las APIs públicas.', 'Las apis públicas.'],
  ])('reads acronyms the Spanish way: %s', (text, expected) => {
    expect(prepareTextForSpeech(text)).toBe(expected)
  })

  it('reads resource paths aloud', () => {
    expect(prepareTextForSpeech('Por ejemplo, POST /productos/42 crea el recurso.')).toBe(
      'Por ejemplo, POST barra productos barra 42 crea el recurso.',
    )
    expect(prepareTextForSpeech('Filtra con GET /productos?categoria=libros.')).toBe(
      'Filtra con GET barra productos con el parámetro categoria igual a libros.',
    )
  })

  it('expands common abbreviations and turns colons into pauses', () => {
    expect(prepareTextForSpeech('Métodos: GET, POST, etc.')).toBe('Métodos, GET, POST, etcétera')
    expect(prepareTextForSpeech('p. ej. un pago')).toBe('por ejemplo un pago')
  })

  it('keeps times and plain words untouched', () => {
    expect(prepareTextForSpeech('La clase es a las 10:30.')).toBe('La clase es a las 10:30.')
    expect(prepareTextForSpeech('La idempotencia importa.')).toBe('La idempotencia importa.')
  })

  it('drops formatting marks and links', () => {
    expect(prepareTextForSpeech('**POST** crea `recursos`, ver https://example.com/doc')).toBe(
      'POST crea recursos, ver el enlace',
    )
  })
})
