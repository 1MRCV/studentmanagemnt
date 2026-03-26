properties([
  parameters([

    choice(
      name: 'ENVIRONMENT',
      choices: ['DEV', 'PRODUCTION'],
      description: 'Select deployment environment'
    ),

    choice(
      name: 'ACTION',
      choices: ['DEPLOY', 'ROLLBACK'],
      description: 'DEPLOY = build and deploy (DEV) or deploy existing artifact (PRODUCTION).'
    ),

    string(
      name: 'BRANCH',
      defaultValue: 'main',
      description: 'Git branch to build'
    ),

    [$class: 'CascadeChoiceParameter',
      name: 'ARTIFACT_BUILD',
      description: 'Select build to deploy or leave default for DEV fresh build',
      choiceType: 'PT_SINGLE_SELECT',
      filterable: true,
      script: [
        $class: 'GroovyScript',
        script: [
          $class: 'SecureGroovyScript',
          sandbox: false,
          script: '''
            import jenkins.model.Jenkins

            def job = Jenkins.instance.getItemByFullName("Test")
            def list = ["-- Build from Git (DEV only) --"]

            job?.getBuilds()?.each { build ->
              if (build.getResult()?.toString() == "SUCCESS") {
                def params = build.getAction(hudson.model.ParametersAction)
                if (params?.getParameter("ENVIRONMENT")?.getValue() == "DEV") {
                  list << "Build_${build.getNumber()}"
                }
              }
            }
            return list
          '''
        ]
      ]
    ]

  ])
])

pipeline {

  agent { label 'windows-agent' }

  environment {
    GIT_REPO = 'https://github.com/1MRCV/studentmanagemnt.git'

    DEV_ARTIFACT_STORAGE  = 'C:\\jenkins-artifacts\\DEV'
    PROD_ARTIFACT_STORAGE = 'C:\\jenkins-artifacts\\PROD'

    DEV_IIS_PATH  = 'C:\\inetpub\\dev'
    PROD_IIS_PATH = 'C:\\inetpub\\prod'

    DEV_APP_POOL  = 'student-dev-pool'
    PROD_APP_POOL = 'student-prod-pool'

    DEV_SITE  = 'student-dev'
    PROD_SITE = 'student-prod'

    DEV_PORT  = '8081'
    PROD_PORT = '8082'

    PUBLISH_FOLDER = 'publish'
  }

  stages {

    stage('Checkout') {
      when { expression { params.ENVIRONMENT == 'DEV' } }
      steps {
        checkout([$class: 'GitSCM',
          branches: [[name: params.BRANCH]],
          userRemoteConfigs: [[url: env.GIT_REPO]]
        ])
      }
    }

    stage('Build & Publish') {
      when { expression { params.ENVIRONMENT == 'DEV' } }
      steps {
        powershell '''
          dotnet restore
          dotnet build --configuration Release
          dotnet publish StudentPortal.Web/StudentPortal.Web.csproj -c Release -o publish
        '''
      }
    }

    stage('Create Artifact') {
      when { expression { params.ENVIRONMENT == 'DEV' } }
      steps {
        powershell '''
          $storage = $env:DEV_ARTIFACT_STORAGE
          if (!(Test-Path $storage)) { New-Item -ItemType Directory -Path $storage }

          $folder = "$storage\\build_$env:BUILD_NUMBER"
          New-Item -ItemType Directory -Path $folder -Force

          Compress-Archive -Path "publish\\*" -DestinationPath "$folder\\artifact.zip" -Force

          Write-Host "Artifact created: $folder"
        '''
      }
    }

    stage('Deploy to IIS') {
      steps {
        powershell '''
          Import-Module WebAdministration

          $envName  = "${params.ENVIRONMENT}"
          $selected = "${params.ARTIFACT_BUILD}"

          $storage = if ($envName -eq "DEV") { $env:DEV_ARTIFACT_STORAGE } else { $env:PROD_ARTIFACT_STORAGE }

          # 🔥 FIXED LOGIC
          if ($envName -eq "DEV" -and $selected.StartsWith("-- Build from Git")) {
              Write-Host "Using latest build..."
              $folder = Get-ChildItem $storage -Directory |
                        Sort-Object LastWriteTime -Descending |
                        Select-Object -First 1
          }
          else {
              $buildNum = $selected.Replace("Build_","")
              $folder = Get-ChildItem $storage -Directory |
                        Where-Object { $_.Name -like "build_$buildNum*" } |
                        Select-Object -First 1
          }

          if ($null -eq $folder) {
              throw "Artifact not found"
          }

          $zip = "$($folder.FullName)\\artifact.zip"

          if (!(Test-Path $zip)) {
              throw "artifact.zip missing"
          }

          if ($envName -eq "DEV") {
              $deploy = $env:DEV_IIS_PATH
              $pool   = $env:DEV_APP_POOL
              $site   = $env:DEV_SITE
              $port   = $env:DEV_PORT
          } else {
              $deploy = $env:PROD_IIS_PATH
              $pool   = $env:PROD_APP_POOL
              $site   = $env:PROD_SITE
              $port   = $env:PROD_PORT
          }

          if (!(Test-Path "IIS:\\AppPools\\$pool")) {
              New-WebAppPool -Name $pool
          }

          if (!(Test-Path "IIS:\\Sites\\$site")) {
              New-Website -Name $site -Port $port -PhysicalPath $deploy -ApplicationPool $pool
          }

          Stop-WebAppPool $pool -ErrorAction SilentlyContinue

          Remove-Item "$deploy\\*" -Recurse -Force -ErrorAction SilentlyContinue

          Expand-Archive $zip -DestinationPath $deploy -Force

          Start-WebAppPool $pool
          Start-Website $site

          Write-Host "Deployment SUCCESS"
        '''
      }
    }

  }

  post {
    success { echo "SUCCESS" }
    failure { echo "FAILED" }
  }
}
