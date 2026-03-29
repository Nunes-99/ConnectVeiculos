export class DocumentoValidator {

  static isValidCPF(cpf: string): boolean {
    if (!cpf) return true; // não obrigatório

    cpf = cpf.replace(/\D/g, '');

    if (cpf.length !== 11) return false;

    // Todos os dígitos iguais
    if (/^(\d)\1{10}$/.test(cpf)) return false;

    // Primeiro dígito verificador
    let soma = 0;
    for (let i = 0; i < 9; i++) {
      soma += parseInt(cpf.charAt(i)) * (10 - i);
    }
    let resto = 11 - (soma % 11);
    let digito1 = resto >= 10 ? 0 : resto;

    if (parseInt(cpf.charAt(9)) !== digito1) return false;

    // Segundo dígito verificador
    soma = 0;
    for (let i = 0; i < 10; i++) {
      soma += parseInt(cpf.charAt(i)) * (11 - i);
    }
    resto = 11 - (soma % 11);
    let digito2 = resto >= 10 ? 0 : resto;

    return parseInt(cpf.charAt(10)) === digito2;
  }

  static isValidCNPJ(cnpj: string): boolean {
    if (!cnpj) return true; // não obrigatório

    cnpj = cnpj.replace(/\D/g, '');

    if (cnpj.length !== 14) return false;

    // Todos os dígitos iguais
    if (/^(\d)\1{13}$/.test(cnpj)) return false;

    // Primeiro dígito verificador
    const pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    let soma = 0;
    for (let i = 0; i < 12; i++) {
      soma += parseInt(cnpj.charAt(i)) * pesos1[i];
    }
    let resto = soma % 11;
    let digito1 = resto < 2 ? 0 : 11 - resto;

    if (parseInt(cnpj.charAt(12)) !== digito1) return false;

    // Segundo dígito verificador
    const pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    soma = 0;
    for (let i = 0; i < 13; i++) {
      soma += parseInt(cnpj.charAt(i)) * pesos2[i];
    }
    resto = soma % 11;
    let digito2 = resto < 2 ? 0 : 11 - resto;

    return parseInt(cnpj.charAt(13)) === digito2;
  }
}
