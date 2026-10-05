// PEÇAS AUTOMOTIVAS — comportamento da interface.
// A CSP proíbe scripts inline: todo comportamento é ligado aqui via atributos data-*.
'use strict';

(() => {
  const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
  const $ = (seletor, raiz = document) => raiz.querySelector(seletor);
  const $$ = (seletor, raiz = document) => [...raiz.querySelectorAll(seletor)];

  const renderizarIcones = () => {
    if (window.lucide?.createIcons) window.lucide.createIcons({ attrs: { 'aria-hidden': 'true' } });
  };

  // ---------------------------------------------------------------- Menu móvel
  const iniciarMenu = () => {
    const botao = $('[data-menu-alternar]');
    const menu = $('[data-menu-movel]');
    if (!botao || !menu) return;
    botao.addEventListener('click', () => {
      const aberto = menu.classList.toggle('hidden') === false;
      botao.setAttribute('aria-expanded', String(aberto));
      botao.setAttribute('aria-label', aberto ? 'Fechar menu' : 'Abrir menu');
    });
  };

  // ---------------------------------------------------------------- Alertas
  const iniciarAlertas = () => {
    $$('[data-fechar-alerta]').forEach((botao) =>
      botao.addEventListener('click', () => botao.closest('[data-alerta]')?.remove()));
  };

  // ---------------------------------------------------------------- Confirmações (data-confirm)
  const iniciarConfirmacoes = () => {
    document.addEventListener('submit', (evento) => {
      const origem = evento.submitter ?? evento.target;
      const mensagem = origem?.dataset?.confirm ?? evento.target.dataset?.confirm;
      if (mensagem && !window.confirm(mensagem)) {
        evento.preventDefault();
        evento.stopImmediatePropagation();
        return;
      }
      // Evita duplo envio (ex.: duplo clique em "Confirmar entrega").
      // O timeout roda depois dos demais handlers (ex.: jQuery Validation), então só desabilita se o envio seguiu.
      if (origem instanceof HTMLButtonElement) {
        window.setTimeout(() => { if (!evento.defaultPrevented) origem.disabled = true; }, 0);
      }
    }, true);
  };

  // ---------------------------------------------------------------- Auto-envio de filtros
  const iniciarAutoEnvio = () => {
    $$('form[data-auto-submit]').forEach((form) => {
      form.addEventListener('change', (evento) => {
        const campo = evento.target;
        if (campo.matches('[data-cascata-montadora]')) {
          const modelo = $(campo.dataset.cascataMontadora, form);
          if (modelo) modelo.value = '';
        }
        if (campo.matches('select, input[type="checkbox"]')) form.requestSubmit();
      });
    });
  };

  // ---------------------------------------------------------------- Cascata Montadora → Modelo
  const iniciarCascata = () => {
    $$('select[data-cascata-montadora]').forEach((montadora) => {
      if (montadora.form?.hasAttribute('data-auto-submit')) return; // o servidor recarrega os modelos
      const modelo = $(montadora.dataset.cascataMontadora);
      if (!modelo) return;

      const preencher = (opcoes, rotuloVazio) => {
        modelo.replaceChildren(new Option(rotuloVazio, ''), ...opcoes.map((m) => new Option(m, m)));
        modelo.disabled = opcoes.length === 0;
      };

      montadora.addEventListener('change', async () => {
        const valor = montadora.value;
        if (!valor) { preencher([], 'Selecione a montadora primeiro'); return; }
        preencher([], 'Carregando modelos…');
        try {
          const resposta = await fetch(`/Catalogo/Modelos?montadora=${encodeURIComponent(valor)}`, {
            headers: { Accept: 'application/json' }, credentials: 'same-origin'
          });
          if (!resposta.ok) throw new Error(String(resposta.status));
          const modelos = await resposta.json();
          preencher(Array.isArray(modelos) ? modelos : [], 'Todos os modelos');
        } catch {
          preencher([], 'Não foi possível carregar');
        }
      });
    });
  };

  // ---------------------------------------------------------------- Barras de progresso (sem style inline)
  const iniciarBarras = () => {
    $$('[data-largura]').forEach((barra) => {
      const pct = Math.max(0, Math.min(100, Number(barra.dataset.largura) || 0));
      barra.style.width = `${pct}%`;
    });
  };

  // ---------------------------------------------------------------- Checkout: recálculo de totais
  const iniciarCheckout = () => {
    const form = $('form[data-checkout]');
    if (!form) return;
    const precoFabrica = Number(form.dataset.precoFabrica) || 0;
    const precoConcessionaria = Number(form.dataset.precoConcessionaria) || 0;
    const campo = $('[data-checkout-quantidade]', form);
    const total = $('[data-checkout-total]', form);
    const concessionaria = $('[data-checkout-concessionaria]', form);
    const economia = $('[data-checkout-economia]', form);
    if (!campo) return;

    const atualizar = () => {
      const max = Number(campo.max) || 99;
      const qtd = Math.max(1, Math.min(max, Math.trunc(Number(campo.value) || 1)));
      if (total) total.textContent = moeda.format(precoFabrica * qtd);
      if (concessionaria) concessionaria.textContent = moeda.format(precoConcessionaria * qtd);
      if (economia) economia.textContent = moeda.format(Math.max(0, precoConcessionaria - precoFabrica) * qtd);
    };
    campo.addEventListener('input', atualizar);
    atualizar();
  };

  // ---------------------------------------------------------------- Admin: prévia de economia
  const iniciarPreviaEconomia = () => {
    const concessionaria = $('[data-preco-concessionaria-campo]');
    const fabrica = $('[data-preco-fabrica-campo]');
    const saida = $('[data-economia-previa]');
    if (!concessionaria || !fabrica || !saida) return;

    const atualizar = () => {
      const c = Number(concessionaria.value);
      const f = Number(fabrica.value);
      if (!(c > 0) || !(f > 0)) { saida.textContent = ''; return; }
      if (f > c) { saida.textContent = 'O preço de fábrica está acima do de concessionária.'; saida.classList.replace('text-emerald-300', 'text-red-300'); return; }
      saida.classList.replace('text-red-300', 'text-emerald-300');
      const pct = ((c - f) / c) * 100;
      saida.textContent = `Economia de ${moeda.format(c - f)} (${pct.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%) para o cliente.`;
    };
    concessionaria.addEventListener('input', atualizar);
    fabrica.addEventListener('input', atualizar);
    atualizar();
  };

  // ---------------------------------------------------------------- Admin: prévia e limite da imagem
  const iniciarPreviaImagem = () => {
    $$('input[type="file"][data-preview-alvo]').forEach((input) => {
      const alvo = $(input.dataset.previewAlvo);
      const vazio = $('[data-preview-vazio]');
      const limite = Number(input.dataset.maxBytes) || 0;
      let urlAnterior = null;

      input.addEventListener('change', () => {
        const arquivo = input.files?.[0];
        input.setCustomValidity('');
        if (!arquivo || !alvo) return;

        if (limite && arquivo.size > limite) {
          input.setCustomValidity('A imagem deve ter no máximo 5 MB.');
          input.reportValidity();
          input.value = '';
          return;
        }
        if (!['image/jpeg', 'image/png', 'image/webp'].includes(arquivo.type)) {
          input.setCustomValidity('Envie uma imagem .jpg, .png ou .webp.');
          input.reportValidity();
          input.value = '';
          return;
        }

        if (urlAnterior) URL.revokeObjectURL(urlAnterior);
        urlAnterior = URL.createObjectURL(arquivo);
        alvo.src = urlAnterior;
        alvo.classList.remove('hidden');
        vazio?.classList.add('hidden');
      });
    });
  };

  // ---------------------------------------------------------------- Mostrar/ocultar senha
  const iniciarAlternarSenha = () => {
    $$('[data-alternar-senha]').forEach((botao) => {
      const campo = $(botao.dataset.alternarSenha);
      if (!campo) return;
      botao.addEventListener('click', () => {
        const mostrar = campo.type === 'password';
        campo.type = mostrar ? 'text' : 'password';
        botao.setAttribute('aria-label', mostrar ? 'Ocultar senha' : 'Mostrar senha');
        botao.replaceChildren(Object.assign(document.createElement('i'), { className: 'h-4 w-4' }));
        botao.firstChild.setAttribute('data-lucide', mostrar ? 'eye-off' : 'eye');
        renderizarIcones();
      });
    });
  };

  // ---------------------------------------------------------------- Máscaras simples
  const mascaras = {
    telefone: (d) => {
      d = d.slice(0, 11);
      if (d.length <= 2) return d.length ? `(${d}` : '';
      if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
      if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
      return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
    },
    documento: (d) => {
      d = d.slice(0, 14);
      if (d.length <= 11) {
        return d.replace(/^(\d{3})(\d)/, '$1.$2').replace(/^(\d{3})\.(\d{3})(\d)/, '$1.$2.$3').replace(/\.(\d{3})(\d)/, '.$1-$2');
      }
      return d.replace(/^(\d{2})(\d)/, '$1.$2').replace(/^(\d{2})\.(\d{3})(\d)/, '$1.$2.$3')
        .replace(/\.(\d{3})(\d)/, '.$1/$2').replace(/(\d{4})(\d)/, '$1-$2');
    }
  };

  const iniciarMascaras = () => {
    $$('input[data-mascara]').forEach((campo) => {
      const aplicar = mascaras[campo.dataset.mascara];
      if (!aplicar) return;
      campo.addEventListener('input', () => { campo.value = aplicar(campo.value.replace(/\D/g, '')); });
    });
  };

  // ---------------------------------------------------------------- PWA
  const registrarServiceWorker = () => {
    if (!('serviceWorker' in navigator) || !window.isSecureContext) return;
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch((erro) => console.warn('Service worker não registrado:', erro));
    });
  };

  document.addEventListener('DOMContentLoaded', () => {
    renderizarIcones();
    iniciarMenu();
    iniciarAlertas();
    iniciarConfirmacoes();
    iniciarAutoEnvio();
    iniciarCascata();
    iniciarBarras();
    iniciarCheckout();
    iniciarPreviaEconomia();
    iniciarPreviaImagem();
    iniciarAlternarSenha();
    iniciarMascaras();
  });
  // Ao voltar pelo histórico (bfcache), reativa botões desabilitados pelo anti duplo envio.
  window.addEventListener('pageshow', (evento) => {
    if (evento.persisted) $$('button[disabled]').forEach((b) => { b.disabled = false; });
  });
  registrarServiceWorker();
})();
