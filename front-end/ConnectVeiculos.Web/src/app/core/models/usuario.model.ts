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
  usuId: number;
  usuNome: string;
  usuEmail: string;
  usuFuncao: string;
  token: string;
  expiration: string;
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
