import { v4 as uuidv4 } from 'uuid'; // Necessário para gerar IDs únicos (UUIDs)

// --- 1. Classes de Estrutura de Workflow (DTOs) ---

/**
 * Representa a posição (X, Y) de um nó no workflow.
 */
export class Position {
    /** @type {number} */
    x;
    /** @type {number} */
    y;

    /**
     * @param {number} x
     * @param {number} y
     */
    constructor(x = 0, y = 0) {
        this.x = x;
        this.y = y;
    }
}

/**
 * Representa um nó (tarefa, ação) dentro de um workflow.
 */
export class WorkflowNode {
    /** @type {string} */
    id;
    /** @type {string} */
    name; // Nome técnico (webhook, httpRequest)
    /** @type {string} */
    type; // Classe C# (MyClone.HttpRequest)
    /** @type {Position} */
    position;

    /** @type {Object<string, any>} */
    parameters;

    /**
     * @param {string} id
     * @param {string} name
     * @param {string} type
     * @param {Position} position
     * @param {Object<string, any>} [parameters={}]
     */
    constructor(id, name, type, position, parameters = {}) {
        this.id = id;
        this.name = name;
        this.type = type;
        this.position = position;
        this.parameters = parameters;
    }
}

export class WorkflowConnection {
    /** @type {string} */
    sourceNodeId;
    /** @type {string} */
    targetNodeId;

    /** @type {string} */
    sourceOutput; // Mapeia outputKey
    /** @type {string} */
    targetInput;  // Mapeia conn.output

    /**
     * @param {string} sourceNodeId
     * @param {string} targetNodeId
     * @param {string} sourceOutput
     * @param {string} targetInput
     */
    constructor(sourceNodeId, targetNodeId, sourceOutput, targetInput) {
        this.sourceNodeId = sourceNodeId;
        this.targetNodeId = targetNodeId;
        this.sourceOutput = sourceOutput;
        this.targetInput = targetInput;
    }
}

/**
 * Contém a estrutura completa de um workflow (nós e conexões).
 */
export class WorkflowData {
    /** @type {WorkflowNode[]} */
    nodes;
    /** @type {WorkflowConnection[]} */
    connections;

    /**
     * @param {WorkflowNode[]} [nodes=[]]
     * @param {WorkflowConnection[]} [connections=[]]
     */
    constructor(nodes = [], connections = []) {
        this.nodes = nodes;
        this.connections = connections;
    }
}

/**
 * Representa uma notificação de webhook recebida.
 */
export class WebhookNotification {
    /** @type {Date} */
    receivedAt;
    /** @type {string} */
    type;
    /** @type {string} */
    action;
    /** @type {string} */
    data; // JSON string ou dado em string

    /**
     * @param {string} [type='']
     * @param {string} [action='']
     * @param {string} [data='']
     */
    constructor(type = '', action = '', data = '') {
        this.receivedAt = new Date();
        this.type = type;
        this.action = action;
        this.data = data;
    }
}

export class SmartAgent {
    /** @type {string} */
    id;
    /** @type {string} */
    name;
    /** @type {string} */
    description;
    /** @type {string} */
    userId;

    /** @type {boolean} */
    isPublic;
    /** @type {string} */
    category;
    /** @type {number} */
    downloads;


    /** @type {WorkflowData | null} */
    workflow;

    /** @type {Date | null} */
    createdAt;
    /** @type {Date | null} */
    updatedAt;
    /** @type {number} */
    price;
    /** @type {string} */
    type;
    /** @type {string} */
    status;

    /**
     * @param {string} [name='']
     * @param {string} [description='']
     * @param {string} [userId='']
     * @param {string} [category='']
     * @param {number} [price=0]
     * @param {string} [type='']
     * @param {string} [status='']
     */
    constructor(name = '', description = '', userId = '', category = '', price = 0, type = '', status = '') {
        this.id = uuidv4();
        this.name = name;
        this.description = description;
        this.userId = userId;
        this.isPublic = false;
        this.category = category;
        this.downloads = 0;
        this.workflow = new WorkflowData();
        this.createdAt = new Date();
        this.updatedAt = null;
        this.price = price;
        this.type = type;
        this.status = status;
    }
}

export class DebugSecurityParameters {
    /** @type {boolean} */
    debugMode = false;
    /** @type {string} */
    logLevel = 'INFO';
    /** @type {boolean} */
    enableSecurityChecks = true;
    /** @type {number} */
    maxLoginAttempts = 5;
    /** @type {string} */
    twoFactorAuthPolicy = 'Mandatory';

    /**
     * @param {boolean} [debugMode=false]
     * @param {string} [logLevel='INFO']
     * @param {boolean} [enableSecurityChecks=true]
     * @param {number} [maxLoginAttempts=5]
     * @param {string} [twoFactorAuthPolicy='Mandatory']
     */
    constructor(
        debugMode = false,
        logLevel = 'INFO',
        enableSecurityChecks = true,
        maxLoginAttempts = 5,
        twoFactorAuthPolicy = 'Mandatory'
    ) {
        this.debugMode = debugMode;
        this.logLevel = logLevel;
        this.enableSecurityChecks = enableSecurityChecks;
        this.maxLoginAttempts = maxLoginAttempts;
        this.twoFactorAuthPolicy = twoFactorAuthPolicy;
    }
}

export class SocialAgentAIParameters {
    /** @type {string} */
    aiModelVersion = 'GPT-4';
    /** @type {number} */
    maxTokens = 4096;
    /** @type {number} */
    temperature = 0.7;
    /** @type {boolean} */
    enableLearning = true;
    /** @type {string} */
    socialPersona = 'Helpful Assistant';

    /**
     * @param {string} [aiModelVersion='GPT-4']
     * @param {number} [maxTokens=4096]
     * @param {number} [temperature=0.7]
     * @param {boolean} [enableLearning=true]
     * @param {string} [socialPersona='Helpful Assistant']
     */
    constructor(
        aiModelVersion = 'GPT-4',
        maxTokens = 4096,
        temperature = 0.7,
        enableLearning = true,
        socialPersona = 'Helpful Assistant'
    ) {
        this.aiModelVersion = aiModelVersion;
        this.maxTokens = maxTokens;
        this.temperature = temperature;
        this.enableLearning = enableLearning;
        this.socialPersona = socialPersona;
    }
}

export class Web3BlockchainParameters {
    /** @type {string} */
    blockchainNetwork = 'Ethereum Mainnet';
    /** @type {string} */
    defaultWalletAddress = '';
    /** @type {number} */
    gasLimit = 21000;
    /** @type {string} */
    smartContractAddress = '';
    /** @type {boolean} */
    enableAutomaticTransactions = false;

    /**
     * @param {string} [blockchainNetwork='Ethereum Mainnet']
     * @param {string} [defaultWalletAddress='']
     * @param {number} [gasLimit=21000]
     * @param {string} [smartContractAddress='']
     * @param {boolean} [enableAutomaticTransactions=false]
     */
    constructor(
        blockchainNetwork = 'Ethereum Mainnet',
        defaultWalletAddress = '',
        gasLimit = 21000,
        smartContractAddress = '',
        enableAutomaticTransactions = false
    ) {
        this.blockchainNetwork = blockchainNetwork;
        this.defaultWalletAddress = defaultWalletAddress;
        this.gasLimit = gasLimit;
        this.smartContractAddress = smartContractAddress;
        this.enableAutomaticTransactions = enableAutomaticTransactions;
    }
}

export class InfraGovernanceParameters {
    /** @type {string} */
    cloudProvider = 'AWS';
    /** @type {string} */
    region = 'us-east-1';
    /** @type {number} */
    autoScaleMinInstances = 2;
    /** @type {number} */
    dailyBackupRetentionDays = 7;
    /** @type {boolean} */
    complianceModeEnabled = true;

    /**
     * @param {string} [cloudProvider='AWS']
     * @param {string} [region='us-east-1']
     * @param {number} [autoScaleMinInstances=2]
     * @param {number} [dailyBackupRetentionDays=7]
     * @param {boolean} [complianceModeEnabled=true]
     */
    constructor(
        cloudProvider = 'AWS',
        region = 'us-east-1',
        autoScaleMinInstances = 2,
        dailyBackupRetentionDays = 7,
        complianceModeEnabled = true
    ) {
        this.cloudProvider = cloudProvider;
        this.region = region;
        this.autoScaleMinInstances = autoScaleMinInstances;
        this.dailyBackupRetentionDays = dailyBackupRetentionDays;
        this.complianceModeEnabled = complianceModeEnabled;
    }
}

export class CodeAndTestParameters {
    /** @type {string} */
    defaultLanguage = 'C#';
    /** @type {boolean} */
    runUnitTestsOnCommit = true;
    /** @type {number} */
    minimumCodeCoverage = 80;
    /** @type {string} */
    ciCdPipeline = 'GitHub Actions';
    /** @type {string} */
    stagingEnvironmentUrl = 'https://staging.app.com';

    /**
     * @param {string} [defaultLanguage='C#']
     * @param {boolean} [runUnitTestsOnCommit=true]
     * @param {number} [minimumCodeCoverage=80]
     * @param {string} [ciCdPipeline='GitHub Actions']
     * @param {string} [stagingEnvironmentUrl='https://staging.app.com']
     */
    constructor(
        defaultLanguage = 'C#',
        runUnitTestsOnCommit = true,
        minimumCodeCoverage = 80,
        ciCdPipeline = 'GitHub Actions',
        stagingEnvironmentUrl = 'https://staging.app.com'
    ) {
        this.defaultLanguage = defaultLanguage;
        this.runUnitTestsOnCommit = runUnitTestsOnCommit;
        this.minimumCodeCoverage = minimumCodeCoverage;
        this.ciCdPipeline = ciCdPipeline;
        this.stagingEnvironmentUrl = stagingEnvironmentUrl;
    }
}