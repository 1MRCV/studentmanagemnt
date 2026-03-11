pipeline {

    agent { label 'windows-agent' }

    parameters {

        choice(
            name: 'ENVIRONMENT',
            choices: ['DEV','PRODUCTION'],
            description: 'Select environment'
        )

        choice(
            name: 'ACTION',
            choices: ['DEPLOY','ROLLBACK'],
            description: 'Deployment action'
        )

        [$class: 'CascadeChoiceParameter',
            name: 'ARTIFACT_BUILD',
            description: 'Select artifact to deploy',
            referencedParameters: 'ENVIRONMENT',
            choiceType: 'PT_SINGLE_SELECT',
            script: [
                $class: 'GroovyScript',
                script: [
                    $class: 'SecureGroovyScript',
                    script: '''
                        def path = ENVIRONMENT == "DEV" ?
                                   "C:/jenkins-artifacts/DEV" :
                                   "C:/jenkins-artifacts/PROD"

                        def dir = new File(path)

                        if(!dir.exists()){
                            return ["No artifacts found"]
                        }

                        def folders = dir.listFiles()
                            .findAll { it.isDirectory() }
                            .sort { -it.lastModified() }
                            .collect { it.name }

                        return folders
                    ''',
                    sandbox: false
                ]
            ]
        ]

        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch to build'
        )
    }

    environment {

        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

        DEV_ARTIFACT_ROOT = "C:\\jenkins-artifacts\\DEV"
        PROD_ARTIFACT_ROOT = "C:\\jenkins-artifacts\\PROD"

        DEV_PATH = "C:\\inetpub\\dev"
        PROD_PATH = "C:\\inetpub\\prod"

        DEV_SITE = "student-dev"
        PROD_SITE = "student-prod"

        DEV_POOL = "student-dev-pool"
        PROD_POOL = "student-prod-pool"

        DEV_PORT = "8081"
        PROD_PORT = "8082"
    }

    stages {

        stage('Set Build Name') {
            steps {
                script {
                    def date = new Date().format("yyyy-MM-dd")
                    currentBuild.displayName = "#${env.BUILD_NUMBER} - ${params.ENVIRONMENT} - ${date}"
                }
            }
        }

        stage('Clean Workspace') {
            steps {
                deleteDir()
            }
        }

        stage('Checkout Code') {
            steps {
                git branch: "${params.BRANCH}",
                url: "${env.GIT_REPO}"
            }
        }

        stage('Restore Dependencies') {

            when {
                expression {
                    params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV'
                }
            }

            steps {
                powershell 'dotnet restore'
            }
        }

        stage('Build Application') {

            when {
                expression {
                    params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV'
                }
            }

            steps {
                powershell 'dotnet build --configuration Release'
            }
        }

        stage('Publish Website') {

            when {
                expression {
                    params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV'
                }
            }

            steps {
                powershell 'dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish'
            }
        }

        stage('Create Artifact Version') {

            when {
                expression {
                    params.ACTION == 'DEPLOY' && params.ENVIRONMENT == 'DEV'
                }
            }

            steps {

                powershell '''

                $build = $env:BUILD_NUMBER
                $date = Get-Date -Format "yyyy-MM-dd"
                $dateTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

                $artifactRoot = "C:\\jenkins-artifacts\\DEV"

                if (!(Test-Path $artifactRoot)) {
                    New-Item -ItemType Directory -Path $artifactRoot
                }

                $buildFolder = "$artifactRoot\\build_${build}_$date"

                New-Item -ItemType Directory -Path $buildFolder -Force

                $zip = "$buildFolder\\artifact.zip"

                Compress-Archive `
                    -Path "publish\\*" `
                    -DestinationPath $zip `
                    -Force

                $commit = git rev-parse --short HEAD
                $branch = git rev-parse --abbrev-ref HEAD

                $info = @"
Application: StudentPortal
Environment: DEV
Build Number: $build
Branch: $branch
Commit: $commit
Build Date: $dateTime
"@

                $info | Out-File "$buildFolder\\build-info.txt"

                Write-Host "Artifact stored at $buildFolder"

                '''
            }
        }

        stage('Deploy Artifact') {

            when {
                expression { params.ACTION == 'DEPLOY' }
            }

            steps {

                powershell """

                Import-Module WebAdministration

                \$envName = "${params.ENVIRONMENT}"
                \$artifactBuild = "${params.ARTIFACT_BUILD}"

                if(\$envName -eq "DEV"){
                    \$artifactRoot = "${env.DEV_ARTIFACT_ROOT}"
                    \$siteName="${env.DEV_SITE}"
                    \$pool="${env.DEV_POOL}"
                    \$path="${env.DEV_PATH}"
                    \$port="${env.DEV_PORT}"
                }
                else{
                    \$artifactRoot = "${env.PROD_ARTIFACT_ROOT}"
                    \$siteName="${env.PROD_SITE}"
                    \$pool="${env.PROD_POOL}"
                    \$path="${env.PROD_PATH}"
                    \$port="${env.PROD_PORT}"
                }

                if(\$artifactBuild -eq ""){

                    Write-Host "Selecting latest artifact..."

                    \$latest = Get-ChildItem \$artifactRoot |
                              Where-Object {\$_.PSIsContainer} |
                              Sort-Object LastWriteTime -Descending |
                              Select-Object -First 1

                    if(!\$latest){
                        throw "No artifacts found"
                    }

                    \$artifactBuild = \$latest.Name
                }

                \$folder = Join-Path \$artifactRoot \$artifactBuild
                \$zipPath = "\$folder\\artifact.zip"

                Write-Host "Deploying \$artifactBuild"

                if (!(Test-Path "IIS:\\AppPools\\\$pool")) {
                    New-WebAppPool -Name \$pool
                }

                if (!(Test-Path \$path)) {
                    New-Item -ItemType Directory -Path \$path
                }

                if (!(Test-Path "IIS:\\Sites\\\$siteName")) {

                    New-Website `
                        -Name \$siteName `
                        -Port \$port `
                        -PhysicalPath \$path `
                        -ApplicationPool \$pool
                }

                \$state = (Get-WebAppPoolState -Name \$pool).Value

                if (\$state -eq "Started") {
                    Stop-WebAppPool \$pool
                    Start-Sleep -Seconds 2
                }

                Remove-Item "\$path\\*" -Recurse -Force -ErrorAction SilentlyContinue

                Expand-Archive `
                    -Path \$zipPath `
                    -DestinationPath \$path `
                    -Force

                Start-WebAppPool \$pool
                Start-Website \$siteName

                Write-Host "Deployment completed"

                """
            }
        }

        stage('Verify Deployment') {

            when {
                expression { params.ACTION == 'DEPLOY' }
            }

            steps {

                powershell """

                Import-Module WebAdministration

                if("${params.ENVIRONMENT}" -eq "DEV"){
                    \$pool="${env.DEV_POOL}"
                }
                else{
                    \$pool="${env.PROD_POOL}"
                }

                \$state = (Get-WebAppPoolState -Name \$pool).Value

                Write-Host "App Pool State: \$state"

                if (\$state -ne "Started") {
                    throw "Deployment verification failed"
                }

                """

            }
        }

    }

    post {

        success {
            echo "Build ${BUILD_NUMBER} deployed successfully."
        }

        failure {
            echo "Deployment failed. Check logs."
        }

    }
}
