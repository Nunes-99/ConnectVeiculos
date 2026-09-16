export interface Usuario {
  usuId: number;
  r_LojId: number;
  r_AcsId: number;
  lojaNome: string;
  acessoNome: string;
  usuNome: string;
  usuCPF: string;
  usuRG: string;
  usuEmail: string;
  usuSenha?: string;
  usuFuncao: string;
  usuSts: boolean;
}

export interface LoginResponse {
  /** Senha gerada pelo sistema: o app exige a troca antes de liberar as telas. */
  trocarSenhaObrigatoria?: boolean;
  usuId: number;
  usuNome: string;
  usuEmail: string;
  usuFuncao: string;
  token: string;
  expiration: string;
  refreshToken?: string;
  refreshExpiration?: string;
  tenantSlug?: string;
  tenantNome?: string;
}

export interface UsuarioInput {
  usuId?: number;
  r_LojId: number;
  lojasIds?: number[];
  r_AcsId: number;
  usuNome: string;
  usuCPF?: string;
  usuRG?: string;
  usuEmail: string;
  usuSenha: string;
  usuFuncao?: string;
  usuSts: boolean;
}
