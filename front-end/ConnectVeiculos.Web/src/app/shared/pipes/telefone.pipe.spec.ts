import { TelefonePipe } from './telefone.pipe';

describe('TelefonePipe', () => {
  const pipe = new TelefonePipe();

  it('formata fixo de 10 digitos', () => {
    expect(pipe.transform('1133334444')).toBe('(11) 3333-4444');
  });

  it('formata celular de 11 digitos', () => {
    expect(pipe.transform('11999998888')).toBe('(11) 99999-8888');
  });

  it('remove o DDI 55 antes de formatar', () => {
    expect(pipe.transform('5511999998888')).toBe('(11) 99999-8888');
  });

  it('ignora caracteres ja mascarados e reformata', () => {
    expect(pipe.transform('(11) 3333-4444')).toBe('(11) 3333-4444');
  });

  it('devolve vazio para null, undefined ou string vazia', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });

  it('devolve o valor original quando nao reconhece o formato', () => {
    // Melhor mostrar o numero cru do que esconder o contato da loja.
    expect(pipe.transform('123')).toBe('123');
    expect(pipe.transform('ramal 42')).toBe('ramal 42');
  });
});
