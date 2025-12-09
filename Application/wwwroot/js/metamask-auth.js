/**
 * MetaMask Integration for Dyson.AI
 * Handles Web3 wallet connection and authentication
 */

class MetaMaskAuth {
    constructor() {
        this.provider = null;
        this.currentAccount = null;
        this.isMetaMaskInstalled = false;
        this.init();
    }

    /**
     * Initialize MetaMask detection
     */
    init() {
        if (typeof window.ethereum !== 'undefined') {
            this.provider = window.ethereum;
            this.isMetaMaskInstalled = true;
            console.log('MetaMask detected!');
            
            // Listen for account changes
            this.provider.on('accountsChanged', (accounts) => {
                this.handleAccountsChanged(accounts);
            });

            // Listen for chain changes
            this.provider.on('chainChanged', (chainId) => {
                console.log('Chain changed to:', chainId);
                window.location.reload();
            });
        } else {
            console.warn('MetaMask not detected');
            this.isMetaMaskInstalled = false;
        }
    }

    async  connectMetaMask() {
        console.log('🦊 [MetaMask] Iniciando conexão...');

        try {
            // Verifica se MetaMask está instalado
            if (typeof window.ethereum === 'undefined') {
                console.error('❌ [MetaMask] MetaMask não detectado');
                alert('MetaMask não instalado! Por favor, instale a extensão MetaMask.');
                return;
            }

            console.log('✓ [MetaMask] MetaMask detectado');

            // Solicita acesso às contas
            console.log('📝 [MetaMask] Solicitando acesso às contas...');
            const accounts = await window.ethereum.request({
                method: 'eth_requestAccounts'
            });

            if (!accounts || accounts.length === 0) {
                console.error('❌ [MetaMask] Nenhuma conta retornada');
                alert('Nenhuma conta encontrada. Desbloqueie o MetaMask.');
                return;
            }

            const walletAddress = accounts[0];
            console.log('✓ [MetaMask] Conta conectada:', walletAddress);

            // Cria mensagem para assinar
            const timestamp = Date.now();
            const message = `Bem-vindo à Dyson.AI!\n\nPor favor, assine esta mensagem para autenticar.\n\nEndereço: ${walletAddress}\nTimestamp: ${timestamp}`;

            console.log('📝 [MetaMask] Mensagem criada:', message);

            // Solicita assinatura
            console.log('✍️ [MetaMask] Solicitando assinatura...');
            const signature = await window.ethereum.request({
                method: 'personal_sign',
                params: [message, walletAddress]
            });

            console.log('✓ [MetaMask] Assinatura recebida:', signature.substring(0, 20) + '...');

            // Prepara dados para envio
            const requestData = {
                walletAddress: walletAddress,
                signature: signature,
                message: message
            };

            console.log('📤 [Backend] Enviando dados para /Account/Web3Login...');
            console.log('Request:', {
                walletAddress: walletAddress,
                signature: signature.substring(0, 20) + '...',
                message: message.substring(0, 50) + '...'
            });

            // Envia para backend
            const response = await fetch('/Account/Web3Login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(requestData)
            });

            console.log('📥 [Backend] Resposta recebida:', response.status, response.statusText);

            // Verifica se a resposta é OK
            if (!response.ok) {
                const errorText = await response.text();
                console.error('❌ [Backend] Erro na resposta:', errorText);
                alert(`Erro na autenticação: ${response.status} - ${response.statusText}`);
                return;
            }

            // Parse da resposta
            const data = await response.json();
            console.log('📥 [Backend] Dados recebidos:', data);

            // Verifica sucesso
            if (data.success) {
                console.log('✅ [Auth] Autenticação bem-sucedida!');
                console.log('👤 Usuário:', data.userName);
                console.log('📧 Email:', data.email);
                console.log('💼 Carteira:', data.walletAddress);
                console.log('🆕 Novo usuário:', data.isNewUser);
                console.log('🔗 Redirect URL:', data.redirectUrl);

                // Mostra mensagem de sucesso
                if (typeof Swal !== 'undefined') {
                    // Se tiver SweetAlert2
                    Swal.fire({
                        icon: 'success',
                        title: data.message,
                        text: data.isNewUser ? 'Bem-vindo à Dyson.AI!' : 'Bem-vindo de volta!',
                        timer: 2000,
                        showConfirmButton: false
                    }).then(() => {
                        console.log('🔄 [Redirect] Redirecionando para:', data.redirectUrl);
                        window.location.href = data.redirectUrl;
                    });
                } else {
                    // Sem SweetAlert2
                    alert(data.message);
                    console.log('🔄 [Redirect] Redirecionando para:', data.redirectUrl);
                    window.location.href = data.redirectUrl;
                }
            } else {
                console.error('❌ [Auth] Falha na autenticação:', data.message);
                alert(data.message || 'Erro na autenticação');
            }

        } catch (error) {
            console.error('❌ [Error] Erro durante autenticação:', error);

            // Erros específicos do MetaMask
            if (error.code === 4001) {
                console.log('ℹ️ [MetaMask] Usuário rejeitou a requisição');
                alert('Você cancelou a requisição no MetaMask.');
            } else if (error.code === -32002) {
                console.log('ℹ️ [MetaMask] Requisição já pendente');
                alert('Já existe uma requisição pendente no MetaMask. Por favor, abra a extensão.');
            } else {
                console.error('❌ [Error] Detalhes:', {
                    message: error.message,
                    code: error.code,
                    stack: error.stack
                });
                alert('Erro ao conectar com MetaMask: ' + error.message);
            }
        }
    }

    /**
     * Check if MetaMask is installed
     */
    checkMetaMaskInstalled() {
        if (!this.isMetaMaskInstalled) {
            this.showError('MetaMask não está instalado. Por favor, instale a extensão do MetaMask.');
            setTimeout(() => {
                window.open('https://metamask.io/download/', '_blank');
            }, 2000);
            return false;
        }
        return true;
    }

    /**
     * Connect to MetaMask wallet
     */
    async connectWallet() {
        if (!this.checkMetaMaskInstalled()) return null;

        try {
            this.showLoading('Conectando à carteira...');

            const accounts = await this.provider.request({
                method: 'eth_requestAccounts'
            });

            this.currentAccount = accounts[0];
            console.log('Connected account:', this.currentAccount);

            this.hideLoading();
            return this.currentAccount;
        } catch (error) {
            this.hideLoading();
            console.error('Error connecting to MetaMask:', error);
            
            if (error.code === 4001) {
                this.showError('Conexão rejeitada. Por favor, aprove a conexão no MetaMask.');
            } else {
                this.showError('Erro ao conectar à carteira: ' + error.message);
            }
            return null;
        }
    }

    /**
     * Get signature message from server
     */
    async getSignatureMessage(walletAddress, action = 'login') {
        try {
            const response = await fetch('/Account/GetWeb3SignatureMessage', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    walletAddress: walletAddress,
                    action: action
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Erro ao obter mensagem');
            }

            const data = await response.json();
            return data.message;
        } catch (error) {
            console.error('Error getting signature message:', error);
            throw error;
        }
    }

    /**
     * Sign message with MetaMask
     */
    async signMessage(message, account) {
        try {
            this.showLoading('Aguardando assinatura...');

            const signature = await this.provider.request({
                method: 'personal_sign',
                params: [message, account]
            });

            this.hideLoading();
            return signature;
        } catch (error) {
            this.hideLoading();
            console.error('Error signing message:', error);
            
            if (error.code === 4001) {
                this.showError('Assinatura rejeitada. Por favor, assine a mensagem no MetaMask.');
            } else {
                this.showError('Erro ao assinar mensagem: ' + error.message);
            }
            throw error;
        }
    }

    /**
     * Authenticate with server
     */
    async authenticateWithServer(walletAddress, signature, message) {
        try {
            const response = await fetch('/Account/Web3Login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    walletAddress: walletAddress,
                    signature: signature,
                    message: message
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Falha na autenticação');
            }

            const data = await response.json();
            return data;
        } catch (error) {
            console.error('Error authenticating:', error);
            throw error;
        }
    }

    /**
     * Complete login flow
     */
    async login() {
        try {
            // Step 1: Connect wallet
            this.showLoading('Conectando à carteira...');
            const account = await this.connectWallet();
            if (!account) return;

            // Step 2: Get signature message
            this.showLoading('Preparando autenticação...');
            const message = await this.getSignatureMessage(account);

            // Step 3: Sign message
            const signature = await this.signMessage(message, account);

            // Step 4: Authenticate with server
            this.showLoading('Autenticando...');
            const result = await this.authenticateWithServer(account, signature, message);

            this.hideLoading();

            if (result.success) {
                this.showSuccess(result.message);
                
                // Redirect after success
                setTimeout(() => {
                    window.location.href = '/Profile/Index';
                }, 1500);
            } else {
                this.showError('Falha na autenticação');
            }
        } catch (error) {
            this.hideLoading();
            console.error('Login error:', error);
            this.showError(error.message || 'Erro ao fazer login com MetaMask');
        }
    }

    /**
     * Handle account changes
     */
    handleAccountsChanged(accounts) {
        if (accounts.length === 0) {
            console.log('Please connect to MetaMask');
            this.currentAccount = null;
        } else if (accounts[0] !== this.currentAccount) {
            this.currentAccount = accounts[0];
            console.log('Account changed to:', this.currentAccount);
            // Optionally reload or update UI
        }
    }

    /**
     * Format wallet address for display
     */
    formatAddress(address) {
        if (!address) return '';
        return `${address.substring(0, 6)}...${address.substring(address.length - 4)}`;
    }

    /**
     * UI Helper: Show loading
     */
    showLoading(message) {
        // Remove existing loader if any
        this.hideLoading();

        const loader = document.createElement('div');
        loader.id = 'metamask-loader';
        loader.className = 'fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50';
        loader.innerHTML = `
            <div class="bg-secondary-dark p-8 rounded-xl border border-gray-700 text-center max-w-sm">
                <div class="animate-spin rounded-full h-16 w-16 border-b-2 border-accent-blue mx-auto mb-4"></div>
                <p class="text-white text-lg">${message}</p>
            </div>
        `;
        document.body.appendChild(loader);
    }

    /**
     * UI Helper: Hide loading
     */
    hideLoading() {
        const loader = document.getElementById('metamask-loader');
        if (loader) {
            loader.remove();
        }
    }

    /**
     * UI Helper: Show error
     */
    showError(message) {
        this.showNotification(message, 'error');
    }

    /**
     * UI Helper: Show success
     */
    showSuccess(message) {
        this.showNotification(message, 'success');
    }

    /**
     * UI Helper: Show notification
     */
    showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `fixed top-4 right-4 p-4 rounded-lg shadow-lg z-50 max-w-md transform transition-all duration-300`;
        
        const colors = {
            success: 'bg-green-600 border-green-500',
            error: 'bg-red-600 border-red-500',
            info: 'bg-blue-600 border-blue-500'
        };

        notification.classList.add(colors[type] || colors.info);
        notification.innerHTML = `
            <div class="flex items-start">
                <div class="flex-1">
                    <p class="text-white font-medium">${message}</p>
                </div>
                <button onclick="this.parentElement.parentElement.remove()" class="ml-4 text-white hover:text-gray-200">
                    <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
                        <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd"></path>
                    </svg>
                </button>
            </div>
        `;

        document.body.appendChild(notification);

        // Auto remove after 5 seconds
        setTimeout(() => {
            notification.style.opacity = '0';
            setTimeout(() => notification.remove(), 300);
        }, 5000);
    }
}

// Initialize globally
window.metaMaskAuth = new MetaMaskAuth();

// Helper function for easy access
window.loginWithMetaMask = () => {
    window.metaMaskAuth.login();
};
