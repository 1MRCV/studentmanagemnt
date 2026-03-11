pipeline {

    agent { label 'windows-agent' }

    parameters {

        choice(
            name: 'ENVIRONMENT',
            choices: ['DEV','PRODUCTION'],
            description: 'Select deployment environment'
        )

        choice(
            name: 'ACTION',
            choices: ['DEPLOY','ROLLBACK'],
            description: 'Deployment action'
        )

        string(
            name: 'ARTIFACT_BUILD',
            defaultValue: '',
            description: 'Artifact folder name (example: build_12_2026-03-11)'
        )

        string(
            name: 'BRANCH',
            defaultValue: 'main',
            description: 'Git branch to build'
        )
    }

    environment {

        GIT_REPO = "https://github.com/1MRCV/studentmanagemnt.git"

        PUBLISH_FOLDER = "publish"

        ARTIFACT_ROOT = "C:\\jenkins-artifacts"

        DEV_ARTIFACT = "C:\\jenkins-artifacts\\DEV"
        PROD_ARTIFACT = "C:\\jenkins-artifacts\\PROD"

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
                    currentBuild.displayName =
                    "#${env.BUILD_NUMBER} - ${params.ENVIRONMENT} - ${date}"
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
                $version = "1.0.$build"

                $commit = git rev-parse --short HEAD
                $branch = git rev-parse --abbrev-ref HEAD

                $artifactRoot = "C:\\jenkins-artifacts\\DEV"

                if (!(Test-Path $artifactRoot)) {
                    New-Item -ItemType Directory -Path $artifactRoot
                }

                $buildFolder = "$artifactRoot\\build_${build}_$date"

                if (!(Test-Path $buildFolder)) {
                    New-Item -ItemType Directory -Path $buildFolder
                }

                $zip = "$buildFolder\\artifact.zip"

                Compress-Archive `
                    -Path "publish\\*" `
                    -DestinationPath $zip `
                    -Force

                $info = @"
Application: StudentPortal
Environment: DEV
Version: $version
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

        stage('Promote DEV Artifact to PROD') {

            when {
                expression {
                    params.ENVIRONMENT == 'PRODUCTION' &&
                    params.ACTION == 'DEPLOY'
                }
            }

            steps {

                powershell """

                \$artifact = "C:\\jenkins-artifacts\\DEV\\${params.ARTIFACT_BUILD}"
                \$target   = "C:\\jenkins-artifacts\\PROD\\${params.ARTIFACT_BUILD}"

                if(!(Test-Path \$artifact)){
                    throw "DEV artifact not found"
                }

                Copy-Item \$artifact -Destination \$target -Recurse -Force

                Write-Host "Artifact promoted from DEV to PROD"

                """

            }
        }

        stage('Deploy Artifact') {

            steps {

                powershell """

                Import-Module WebAdministration

                \$envName = "${params.ENVIRONMENT}"
                \$artifactBuild = "${params.ARTIFACT_BUILD}"

                if(\$envName -eq "DEV"){

                    \$artifactRoot="C:\\jenkins-artifacts\\DEV"
                    \$siteName="${env.DEV_SITE}"
                    \$pool="${env.DEV_POOL}"
                    \$path="${env.DEV_PATH}"
                    \$port="${env.DEV_PORT}"

                } else {

                    \$artifactRoot="C:\\jenkins-artifacts\\PROD"
                    \$siteName="${env.PROD_SITE}"
                    \$pool="${env.PROD_POOL}"
                    \$path="${env.PROD_PATH}"
                    \$port="${env.PROD_PORT}"

                }

                if(\$artifactBuild -eq ""){
                    throw "Artifact name must be provided"
                }

                \$folder = Join-Path \$artifactRoot \$artifactBuild

                if(!(Test-Path \$folder)){
                    throw "Artifact not found: \$artifactBuild"
                }

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
